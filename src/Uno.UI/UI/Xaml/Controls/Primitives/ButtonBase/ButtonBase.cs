﻿using Uno.Client;
using Uno.Extensions;
using System;
using System.Collections.Generic;
using Uno.Disposables;
using System.Text;
using System.Windows.Input;
using Windows.UI.Xaml.Input;
using Uno.Logging;
#if XAMARIN_IOS
using View = UIKit.UIView;
#elif __MACOS__
using View = AppKit.NSView;
#elif __ANDROID__
using View = Android.Views.View;
#else
using View = Windows.UI.Xaml.UIElement;
#endif

namespace Windows.UI.Xaml.Controls.Primitives
{
	public partial class ButtonBase : ContentControl
	{
		static ButtonBase()
		{
			IsEnabledProperty.OverrideMetadata(
				typeof(ButtonBase),
				new FrameworkPropertyMetadata(
					defaultValue: true,
					propertyChangedCallback: null,
					coerceValueCallback: CoerceIsEnabled
				)
			);
		}

		private readonly SerialDisposable _commandCanExecute = new SerialDisposable();

		public
#if XAMARIN_ANDROID
			new
#endif
			event RoutedEventHandler Click;

		public ButtonBase()
		{
			InitializeProperties();
		}

		private void InitializeProperties()
		{
			OnIsEnabledChanged(false, IsEnabled);
			PartialInitializeProperties();
		}

		partial void PartialInitializeProperties();

		#region Command (DP)
		public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
			"Command", typeof(ICommand), typeof(ButtonBase), new PropertyMetadata(default(ICommand), OnCommandChanged));

		public ICommand Command
		{
			get { return (ICommand)this.GetValue(CommandProperty); }
			set { this.SetValue(CommandProperty, value); }
		}

		private static void OnCommandChanged(object dependencyobject, DependencyPropertyChangedEventArgs args)
		{
			((ButtonBase)dependencyobject).OnCommandChanged(args.NewValue as ICommand);
		}
		#endregion

		partial void RegisterEvents();

		private void OnCommandChanged(ICommand newCommand)
		{
			_commandCanExecute.Disposable = null;

			if (newCommand != null)
			{
				EventHandler handler = (s, e) => OnCanExecuteChanged();

				newCommand.CanExecuteChanged += handler;

				_commandCanExecute.Disposable = Disposable
					.Create(() =>
					{
						newCommand.CanExecuteChanged -= handler;
					}
				);
			}

			OnCanExecuteChanged();
		}

		private void OnCanExecuteChanged()
		{
			this.CoerceValue(IsEnabledProperty);
		}

		private static object CoerceIsEnabled(DependencyObject dependencyObject, object baseValue)
		{
			if (dependencyObject is ButtonBase buttonBase
				&& buttonBase.Command != null
				&& !buttonBase.Command.CanExecute(buttonBase.CommandParameter))
			{
				return false;
			}

			return baseValue;
		}

		protected override void OnIsEnabledChanged(bool oldValue, bool newValue)
		{
			base.OnIsEnabledChanged(oldValue, newValue);
			OnIsEnabledChangedPartial(oldValue, newValue);
		}

		partial void OnIsEnabledChangedPartial(bool oldValue, bool newValue);

		public override View ContentTemplateRoot
		{
			get
			{
				return base.ContentTemplateRoot;
			}
			protected set
			{
				base.ContentTemplateRoot = value;

				RegisterEvents();
			}
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			RegisterEvents();
		}

		private void OnClick(PointerRoutedEventArgs args = null)
		{
			Click?.Invoke(this, RoutedEventArgs.Empty);

			try
			{
				if (this.Log().IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					this.Log().Debug("Raising command");
				}

				Command.ExecuteIfPossible(CommandParameter);
			}
			catch (Exception e)
			{
				this.Log().Error("Failed to execute command", e);
			}
		}

		protected override void OnPointerPressed(PointerRoutedEventArgs args)
		{
			base.OnPointerPressed(args);

			IsPointerOver = true;
			IsPointerPressed = true;

#if !__WASM__
			// TODO: Remove when Focus is implemented properly.
			// Focus the button when pressed down to ensure that any focused TextBox loses focus 
			// so that TwoWay binding (to source) is triggered before the button is released and Click is raised.
			Focus(FocusState.Pointer);
#endif
		}

		protected override void OnPointerReleased(PointerRoutedEventArgs args)
		{
			base.OnPointerReleased(args);

			IsPointerOver = false;
			IsPointerPressed = false;
		}

		protected override void OnPointerMoved(PointerRoutedEventArgs args)
		{
			base.OnPointerMoved(args);
		}

		protected override void OnPointerCanceled(PointerRoutedEventArgs args)
		{
			base.OnPointerCanceled(args);

			IsPointerOver = false;
			IsPointerPressed = false;
		}

		protected override void OnPointerEntered(PointerRoutedEventArgs args)
		{
			base.OnPointerEntered(args);

			IsPointerOver = true;
		}

		protected override void OnPointerExited(PointerRoutedEventArgs args)
		{
			base.OnPointerExited(args);

			IsPointerOver = false;
		}

		#region CommandParameter

		public object CommandParameter
		{
			get { return (object)GetValue(CommandParameterProperty); }
			set { SetValue(CommandParameterProperty, value); }
		}

		public static global::Windows.UI.Xaml.DependencyProperty CommandParameterProperty { get; } =
			Windows.UI.Xaml.DependencyProperty.Register(
				"CommandParameter", typeof(object),
				typeof(global::Windows.UI.Xaml.Controls.Primitives.ButtonBase),
				new FrameworkPropertyMetadata(default(object), OnCommandParameterChanged));

		private static void OnCommandParameterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			((ButtonBase)dependencyObject)?.CoerceValue(IsEnabledProperty);
		}

		#endregion

		// Might be changed if the method does not conflict in UnoViewGroup.
		internal override bool IsViewHit()
		{
			// Overrides the need for a non-null Background (required by base.IsViewHit)
			return true;
		}

		// Allows native buttons (e.g., UIBarButtonItem, IMenuItem) to raise clicks on their associated AppBarButton.
		internal void RaiseClick(PointerRoutedEventArgs args = null)
		{
			OnClick(args);
		}

		internal void AutomationPeerClick()
		{
			OnClick();
		}
	}
}
