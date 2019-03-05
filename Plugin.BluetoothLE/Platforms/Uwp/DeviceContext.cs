﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Acr.Logging;
using NC = Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic;


namespace Plugin.BluetoothLE
{
    public class DeviceContext
    {
        readonly object syncLock;
        readonly AdapterContext adapterContext;
        readonly IList<GattCharacteristic> subscribers;
        readonly Subject<ConnectionStatus> connSubject;
        readonly ulong bluetoothAddress;


        public DeviceContext(AdapterContext adapterContext,
                             IDevice device,
                             BluetoothLEDevice native)
        {
            this.syncLock = new object();
            this.connSubject = new Subject<ConnectionStatus>();
            this.adapterContext = adapterContext;
            this.subscribers = new List<GattCharacteristic>();
            this.Device = device;
            this.NativeDevice = native;
            this.bluetoothAddress = native.BluetoothAddress;
        }


        public IDevice Device { get; }
        public BluetoothLEDevice NativeDevice { get; private set; }
        public IObservable<ConnectionStatus> WhenStatusChanged()
        {
            if (this.NativeDevice != null)
            {
                // Ensure we don't end up being triggered multiple times by the event (it is tottaly leagal to call -= even it there is no previous +=)
                // we hook this up in Connect() and WhenStatusChanged()
                this.NativeDevice.ConnectionStatusChanged -= this.OnNativeConnectionStatusChanged;
                this.NativeDevice.ConnectionStatusChanged += this.OnNativeConnectionStatusChanged;
            }
            return this.connSubject.StartWith(this.Status);
        }

        public async Task Connect()
        {
            if (this.NativeDevice != null && this.NativeDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                return;

            this.connSubject.OnNext(ConnectionStatus.Connecting);
            this.NativeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(this.bluetoothAddress);
            // Ensure we don't end up being triggered multiple times by the event (it is tottaly leagal to call -= even it there is no previous +=)
            // we hook this up in Connect() and WhenStatusChanged()
            this.NativeDevice.ConnectionStatusChanged -= this.OnNativeConnectionStatusChanged;
            this.NativeDevice.ConnectionStatusChanged += this.OnNativeConnectionStatusChanged;
            await this.NativeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached); // HACK: kick the connection on
        }


        public async Task Disconnect()
        {
            if (this.NativeDevice == null)
                return;

            this.connSubject.OnNext(ConnectionStatus.Disconnecting);
            foreach (var ch in this.subscribers)
            {
                try
                {
                    await ch.Disconnect();
                }
                catch (Exception e)
                {
                    Log.Info(BleLogCategory.Device, "Disconnect Error - " + e);
                }
            }
            this.subscribers.Clear();

            this.adapterContext.RemoveDevice(this.NativeDevice.BluetoothAddress);
            this.NativeDevice.ConnectionStatusChanged -= this.OnNativeConnectionStatusChanged;
            this.NativeDevice?.Dispose();
            this.NativeDevice = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            this.connSubject.OnNext(ConnectionStatus.Disconnected);
        }


        public void SetNotifyCharacteristic(GattCharacteristic characteristic)
        {
            lock (this.syncLock)
            {
                if (characteristic.IsNotifying)
                {
                    this.subscribers.Add(characteristic);
                }
                else
                {
                    this.subscribers.Remove(characteristic);
                }
            }
        }


        public ConnectionStatus Status
        {
            get
            {
                if (this.NativeDevice == null)
                    return ConnectionStatus.Disconnected;

                switch (this.NativeDevice.ConnectionStatus)
                {
                    case BluetoothConnectionStatus.Connected:
                        return ConnectionStatus.Connected;

                    default:
                        return ConnectionStatus.Disconnected;
                }
            }
        }


        void OnNativeConnectionStatusChanged(BluetoothLEDevice sender, object args) =>
            this.connSubject.OnNext(this.Status);
    }
}