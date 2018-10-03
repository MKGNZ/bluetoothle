﻿using System;
using Plugin.BluetoothLE;
using Foundation;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;


namespace Samples.iOS
{
    [Register("AppDelegate")]
    public partial class AppDelegate : FormsApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            Forms.Init();
            CrossBleAdapter.Init(BleAdapterConfiguration.DefaultBackgroudingConfig);
            this.LoadApplication(new App(new PlatformInitializer()));

            //UIApplication.SharedApplication.IdleTimerDisabled = false;
            return base.FinishedLaunching(app, options);
        }
    }
}
