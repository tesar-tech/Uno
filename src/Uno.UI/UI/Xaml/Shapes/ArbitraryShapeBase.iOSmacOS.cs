﻿using CoreGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Uno.Extensions;
using CoreAnimation;
using Uno.Disposables;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Uno.Logging;

#if __IOS__
using UIKit;
using _Color = UIKit.UIColor;
#elif __MACOS__
using _Color = AppKit.NSColor;
#endif

namespace Windows.UI.Xaml.Shapes
{
	public abstract partial class ArbitraryShapeBase
	{
		// Drawing scale
		private nfloat _scaleX;
		private nfloat _scaleY;

		public ArbitraryShapeBase()
		{

#if __IOS__
			ClipsToBounds = true;
#endif
		}

		protected override void OnBackgroundChanged(DependencyPropertyChangedEventArgs e)
		{
			// Don't call base, we need to keep UIView.BackgroundColor set to transparent
			RefreshShape();
		}

		protected abstract CGPath GetPath();

		internal override void OnLayoutUpdated()
		{
			base.OnLayoutUpdated();
			var size = SizeFromUISize(Bounds.Size);
			RefreshShape();
		}

		protected override void OnLoaded()
		{
			base.OnLoaded();

			RefreshShape();
		}

		private CGRect GetActualSize()
		{
			return Bounds;
		}

		private IDisposable BuildDrawableLayer()
		{
			if (Bounds != CGRect.Empty)
			{
				var newLayer = CreateLayer();

				if (newLayer != null)
				{
					Layer.AddSublayer(newLayer);

					return Disposable.Create(() => newLayer.RemoveFromSuperLayer());
				}
			}

			return Disposable.Empty;
		}

		private CALayer CreateLayer()
		{
			var path = this.GetPath();

			if (path == null)
			{
				return null;
			}

			var pathBounds = path.PathBoundingBox;

			if (
				nfloat.IsInfinity(pathBounds.Left) 
				|| nfloat.IsInfinity(pathBounds.Left)
			)
			{
				if (this.Log().IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					this.Log().Debug($"Ignoring path with invalid bounds {pathBounds}");
				}

				return null;
			}

			var transform = CGAffineTransform.MakeIdentity();

			var scaleX = _scaleX;
			var scaleY = _scaleY;
			switch (this.Stretch)
			{
				case Stretch.Fill:
				case Stretch.None:
					break;
				case Stretch.Uniform:
					scaleX = (nfloat)Math.Min(_scaleX, _scaleY);
					scaleY = scaleX;
					break;
				case Stretch.UniformToFill:
					scaleX = (nfloat)Math.Max(_scaleX, _scaleY);
					scaleY = scaleX;
					break;
			}

			transform = CGAffineTransform.MakeScale(scaleX, scaleY);

			if (Stretch != Stretch.None)
			{
				// When stretching, we can't use 0,0 as the origin, but must instead
				// use the path's bounds.
				transform.Translate(-pathBounds.Left * scaleX, -pathBounds.Top * scaleY);
			}

			if (!ShouldPreserveOrigin)
			{
				//We need to translate the shape to take in account the stroke thickness
				transform.Translate((nfloat)ActualStrokeThickness * 0.5f, (nfloat)ActualStrokeThickness * 0.5f);
			}

			if (nfloat.IsNaN(transform.x0) || nfloat.IsNaN(transform.y0) ||
				nfloat.IsNaN(transform.xx) || nfloat.IsNaN(transform.yy) ||
				nfloat.IsNaN(transform.xy) || nfloat.IsNaN(transform.yx)
			)
			{
				//transformedPath creation will crash natively if the transform contains NaNs
				throw new InvalidOperationException($"transform {transform} contains NaN values, transformation will fail.");
			}

			var colorFill = this.Fill as SolidColorBrush ?? SolidColorBrushHelper.Transparent;
			var imageFill = this.Fill as ImageBrush;
			var stroke = this.Stroke as SolidColorBrush ?? SolidColorBrushHelper.Transparent;

			var layer = new CAShapeLayer()
			{
				Path = new CGPath(path, transform),
				StrokeColor = stroke.ColorWithOpacity,
				LineWidth = (nfloat)ActualStrokeThickness,
			};

			if (colorFill != null)
			{
				layer.FillColor = colorFill.ColorWithOpacity;
			}

			if (imageFill != null)
			{
				var fillMask = new CAShapeLayer()
				{
					Path = path,
					Frame = Bounds,
					// We only use the fill color to create the mask area
					FillColor = _Color.White.CGColor,
				};

				CreateImageBrushLayers(
					layer,
					imageFill,
					fillMask
				);
			}

			if (StrokeDashArray != null)
			{
				var pattern = StrokeDashArray.Select(d => (global::Foundation.NSNumber)d).ToArray();

				layer.LineDashPhase = 0; // Starting position of the pattern
				layer.LineDashPattern = pattern;

			}

			return layer;
		}

		private void CreateImageBrushLayers(CALayer layer, ImageBrush imageBrush, CAShapeLayer fillMask)
		{

			var uiImage = imageBrush.ImageSource.ImageData;

			if (uiImage == null)
			{
				return;
			}

			// This layer is the one we apply the mask on. It's the full size of the shape because the mask is as well.
			var imageContainerLayer = new CALayer
			{
				Frame = new CGRect(0, 0, Bounds.Width, Bounds.Height),
				Mask = fillMask,
				BackgroundColor = new CGColor(0, 0, 0, 0),
				MasksToBounds = true,
			};

			// The ImageBrush.Stretch will tell us the SIZE of the image we need for the layer
			var aspectRatio = (double)(uiImage.Size.Width / uiImage.Size.Height);
			CGSize imageSize;
			switch (imageBrush.Stretch)
			{
				default:
				case Stretch.Fill:
					imageSize = Bounds.Size;
					break;
				case Stretch.None:
					imageSize = uiImage.Size;
					break;
				case Stretch.Uniform:
					var width = Math.Min(Bounds.Width, Bounds.Height * aspectRatio);
					var height = width / aspectRatio;
					imageSize = new CGSize(width, height);
					break;
				case Stretch.UniformToFill:
					width = Math.Max(Bounds.Width, Bounds.Height * aspectRatio);
					height = width / aspectRatio;
					imageSize = new CGSize(width, height);
					break;
			}

			// The ImageBrush.AlignementX/Y will tell us the LOCATION we need for the layer
			double deltaX;
			switch (imageBrush.AlignmentX)
			{
				default:
				case AlignmentX.Center:
					deltaX = (double)(Bounds.Width - imageSize.Width) * 0.5f;
					break;
				case AlignmentX.Left:
					deltaX = 0;
					break;
				case AlignmentX.Right:
					deltaX = (double)(Bounds.Width - imageSize.Width);
					break;
			}

			double deltaY;
			switch (imageBrush.AlignmentY)
			{
				default:
				case AlignmentY.Center:
					deltaY = (double)(Bounds.Height - imageSize.Height) * 0.5f;
					break;
				case AlignmentY.Top:
					deltaY = 0;
					break;
				case AlignmentY.Bottom:
					deltaY = (double)(Bounds.Height - imageSize.Height);
					break;
			}

			var imageFrame = new CGRect(new CGPoint(deltaX, deltaY), imageSize);

			// This is the layer with the actual image in it. Its frame is the inside of the border.
			var imageLayer = new CALayer
			{
				Contents = uiImage.CGImage,
				Frame = imageFrame,
				MasksToBounds = true,
			};

			imageContainerLayer.AddSublayer(imageLayer);
			layer.AddSublayer(imageContainerLayer);
		}

		protected override Size MeasureOverride(Size size)
		{
			var path = GetPath();
			if (path == null)
			{
				return default(Size);
			}
			var bounds = path.PathBoundingBox;

			var pathWidth = bounds.Width;
			var pathHeight = bounds.Height;

			if (ShouldPreserveOrigin)
			{
				pathWidth += bounds.X;
				pathHeight += bounds.Y;
			}

			var availableWidth = size.Width;
			var availableHeight = size.Height;

			var userWidth = this.Width;
			var userHeight = this.Height;

			var controlWidth = availableWidth <= 0 ? userWidth : availableWidth;
			var controlHeight = availableHeight <= 0 ? userHeight : availableHeight;

			// Default values
			var calculatedWidth = LimitWithUserSize(controlWidth, userWidth, pathWidth);
			var calculatedHeight = LimitWithUserSize(controlHeight, userHeight, pathHeight);


			_scaleX = (nfloat)(calculatedWidth - (nfloat)this.ActualStrokeThickness) / pathWidth;
			_scaleY = (nfloat)(calculatedHeight - (nfloat)this.ActualStrokeThickness) / pathHeight;

			//Make sure that we have a valid scale if both of them are not set
			if (double.IsInfinity((double)_scaleX) &&
			   double.IsInfinity((double)_scaleY))
			{
				_scaleX = 1;
				_scaleY = 1;
			}

			// Here we will override some of the default values
			switch (this.Stretch)
			{
				// If the Stretch is None, the drawing is not the same size as the control
				case Stretch.None:
					_scaleX = 1;
					_scaleY = 1;
					calculatedWidth = (double)pathWidth;
					calculatedHeight = (double)pathHeight;
					break;
				case Stretch.Fill:
					if (double.IsInfinity((double)_scaleY))
					{
						_scaleY = 1;
					}
					if (double.IsInfinity((double)_scaleX))
					{
						_scaleX = 1;
					}
					calculatedWidth = (double)pathWidth * (double)_scaleX;
					calculatedHeight = (double)pathHeight * (double)_scaleY;

					break;
				// Override the _calculated dimensions if the stretch is Uniform or UniformToFill
				case Stretch.Uniform:
					double scale = (double)Math.Min(_scaleX, _scaleY);
					calculatedWidth = (double)pathWidth * scale;
					calculatedHeight = (double)pathHeight * scale;
					break;
				case Stretch.UniformToFill:
					scale = (double)Math.Max(_scaleX, _scaleY);
					calculatedWidth = (double)pathWidth * scale;
					calculatedHeight = (double)pathHeight * scale;
					break;
			}

			calculatedWidth += (double)this.ActualStrokeThickness;
			calculatedHeight += (double)this.ActualStrokeThickness;

			return new Size(calculatedWidth, calculatedHeight);
		}
	}
}
