using System;
using System.Runtime.InteropServices;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ điều khiển âm lượng hệ thống qua Windows Core Audio COM API (IAudioEndpointVolume)
    /// Giúp điều chỉnh âm lượng trực tiếp mà không kích hoạt System Media Transport Controls Flyout của Windows.
    /// </summary>
    public static class VolumeController
    {
        private static readonly Guid IID_IAudioEndpointVolume = typeof(CoreAudioApi.IAudioEndpointVolume).GUID;
        private static readonly object _syncLock = new object();
        private static CoreAudioApi.IAudioEndpointVolume _cachedEndpoint;

        private static CoreAudioApi.IAudioEndpointVolume GetOrCreateEndpoint()
        {
            if (_cachedEndpoint != null)
            {
                return _cachedEndpoint;
            }

            lock (_syncLock)
            {
                if (_cachedEndpoint != null) return _cachedEndpoint;

                CoreAudioApi.IMMDeviceEnumerator enumerator = null;
                CoreAudioApi.IMMDevice device = null;
                try
                {
                    enumerator = (CoreAudioApi.IMMDeviceEnumerator)new CoreAudioApi.MMDeviceEnumeratorComObject();
                    if (enumerator.GetDefaultAudioEndpoint(CoreAudioApi.eRender, CoreAudioApi.eMultimedia, out device) == 0 && device != null)
                    {
                        var guid = IID_IAudioEndpointVolume;
                        if (device.Activate(ref guid, CoreAudioApi.CLSCTX_ALL, IntPtr.Zero, out var obj) == 0 && obj is CoreAudioApi.IAudioEndpointVolume endpoint)
                        {
                            _cachedEndpoint = endpoint;
                            return _cachedEndpoint;
                        }
                    }
                }
                catch
                {
                    // Bỏ qua lỗi truy cập COM audio endpoint
                }
                finally
                {
                    if (device != null)
                    {
                        try { Marshal.ReleaseComObject(device); } catch { }
                    }
                    if (enumerator != null)
                    {
                        try { Marshal.ReleaseComObject(enumerator); } catch { }
                    }
                }
            }
            return null;
        }

        public static void InvalidateEndpoint()
        {
            lock (_syncLock)
            {
                if (_cachedEndpoint != null)
                {
                    try { Marshal.ReleaseComObject(_cachedEndpoint); } catch { }
                    _cachedEndpoint = null;
                }
            }
        }

        public static float GetVolume()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var endpoint = GetOrCreateEndpoint();
                    if (endpoint != null)
                    {
                        endpoint.GetMasterVolumeLevelScalar(out float level);
                        return level;
                    }
                }
                catch (COMException)
                {
                    InvalidateEndpoint();
                }
                catch { break; }
            }
            return 0.5f;
        }

        public static bool IsMuted()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var endpoint = GetOrCreateEndpoint();
                    if (endpoint != null)
                    {
                        endpoint.GetMute(out bool isMuted);
                        return isMuted;
                    }
                }
                catch (COMException)
                {
                    InvalidateEndpoint();
                }
                catch { break; }
            }
            return false;
        }

        public static (float volume, bool isMuted) AdjustVolume(float deltaPercent)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var endpoint = GetOrCreateEndpoint();
                    if (endpoint != null)
                    {
                        Guid context = Guid.Empty;
                        endpoint.GetMasterVolumeLevelScalar(out float current);
                        endpoint.GetMute(out bool isMuted);

                        // Tự động hủy tắt tiếng nếu người dùng tăng âm lượng
                        if (isMuted && deltaPercent > 0)
                        {
                            endpoint.SetMute(false, ref context);
                            isMuted = false;
                        }

                        float newVol = (float)Math.Round(Math.Max(0.0f, Math.Min(1.0f, current + (deltaPercent / 100.0f))), 4);
                        endpoint.SetMasterVolumeLevelScalar(newVol, ref context);
                        return (newVol, isMuted);
                    }
                }
                catch (COMException)
                {
                    InvalidateEndpoint();
                }
                catch { break; }
            }
            return (0.5f, false);
        }

        public static (float volume, bool isMuted) ToggleMute()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var endpoint = GetOrCreateEndpoint();
                    if (endpoint != null)
                    {
                        Guid context = Guid.Empty;
                        endpoint.GetMasterVolumeLevelScalar(out float current);
                        endpoint.GetMute(out bool isMuted);

                        bool newMute = !isMuted;
                        endpoint.SetMute(newMute, ref context);
                        return (current, newMute);
                    }
                }
                catch (COMException)
                {
                    InvalidateEndpoint();
                }
                catch { break; }
            }
            return (0.5f, false);
        }

        public static void Cleanup()
        {
            InvalidateEndpoint();
        }

        public static string ToggleDefaultAudioDevice(int dataFlow)
        {
            CoreAudioApi.IMMDeviceEnumerator enumerator = null;
            CoreAudioApi.IMMDeviceCollection collection = null;
            CoreAudioApi.IMMDevice defaultDevice = null;

            try
            {
                enumerator = (CoreAudioApi.IMMDeviceEnumerator)new CoreAudioApi.MMDeviceEnumeratorComObject();

                if (enumerator.EnumAudioEndpoints(dataFlow, CoreAudioApi.DEVICE_STATE_ACTIVE, out collection) != 0 || collection == null)
                {
                    return null;
                }

                collection.GetCount(out uint count);
                if (count == 0) return null;

                var deviceList = new System.Collections.Generic.List<(string id, string name)>();

                for (uint i = 0; i < count; i++)
                {
                    CoreAudioApi.IMMDevice dev = null;
                    try
                    {
                        if (collection.Item(i, out dev) == 0 && dev != null)
                        {
                            dev.GetId(out string devId);
                            string name = GetDeviceFriendlyName(dev) ?? devId;
                            if (!string.IsNullOrEmpty(devId))
                            {
                                deviceList.Add((devId, name));
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        if (dev != null)
                        {
                            try { Marshal.ReleaseComObject(dev); } catch { }
                        }
                    }
                }

                if (deviceList.Count == 0) return null;

                string currentDefaultId = null;
                if (enumerator.GetDefaultAudioEndpoint(dataFlow, CoreAudioApi.eConsole, out defaultDevice) == 0 && defaultDevice != null)
                {
                    defaultDevice.GetId(out currentDefaultId);
                }

                int currentIndex = -1;
                if (!string.IsNullOrEmpty(currentDefaultId))
                {
                    currentIndex = deviceList.FindIndex(d => string.Equals(d.id, currentDefaultId, StringComparison.OrdinalIgnoreCase));
                }

                int nextIndex = (currentIndex + 1) % deviceList.Count;
                var targetDevice = deviceList[nextIndex];

                SetDefaultEndpoint(targetDevice.id, CoreAudioApi.eConsole);
                SetDefaultEndpoint(targetDevice.id, CoreAudioApi.eMultimedia);
                SetDefaultEndpoint(targetDevice.id, CoreAudioApi.eCommunications);

                if (dataFlow == CoreAudioApi.eRender)
                {
                    InvalidateEndpoint();
                }

                return targetDevice.name;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VolumeController] Error switching default audio device: {ex.Message}");
                return null;
            }
            finally
            {
                if (defaultDevice != null)
                {
                    try { Marshal.ReleaseComObject(defaultDevice); } catch { }
                }
                if (collection != null)
                {
                    try { Marshal.ReleaseComObject(collection); } catch { }
                }
                if (enumerator != null)
                {
                    try { Marshal.ReleaseComObject(enumerator); } catch { }
                }
            }
        }

        private static string GetDeviceFriendlyName(CoreAudioApi.IMMDevice device)
        {
            CoreAudioApi.IPropertyStore store = null;
            try
            {
                if (device.OpenPropertyStore(CoreAudioApi.STGM_READ, out store) == 0 && store != null)
                {
                    var key = new CoreAudioApi.PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
                    if (store.GetValue(ref key, out CoreAudioApi.PROPVARIANT propVar) == 0)
                    {
                        string val = propVar.GetString();
                        CoreAudioApi.PropVariantClear(ref propVar);
                        return val;
                    }
                }
            }
            catch { }
            finally
            {
                if (store != null)
                {
                    try { Marshal.ReleaseComObject(store); } catch { }
                }
            }
            return null;
        }

        private static void SetDefaultEndpoint(string deviceId, int role)
        {
            try
            {
                var policyConfig = new CoreAudioApi.PolicyConfigClient();
                if (policyConfig is CoreAudioApi.IPolicyConfig pConfig)
                {
                    pConfig.SetDefaultEndpoint(deviceId, role);
                }
                else if (policyConfig is CoreAudioApi.IPolicyConfigWin10 pConfigWin10)
                {
                    pConfigWin10.SetDefaultEndpoint(deviceId, role);
                }
                else if (policyConfig is CoreAudioApi.IPolicyConfigVista pConfigVista)
                {
                    pConfigVista.SetDefaultEndpoint(deviceId, role);
                }
            }
            catch { }
        }
    }

    internal static class CoreAudioApi
    {
        public const int CLSCTX_ALL = 23;
        public const int eRender = 0;
        public const int eCapture = 1;
        public const int eConsole = 0;
        public const int eMultimedia = 1;
        public const int eCommunications = 2;
        public const int DEVICE_STATE_ACTIVE = 1;
        public const int STGM_READ = 0;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        public class MMDeviceEnumeratorComObject { }

        [ComImport]
        [Guid("8706226F-26AA-4AC2-920A-C380456E3D31")]
        public class PolicyConfigClient { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            int RegisterEndpointNotificationCallback(IntPtr pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [Guid("0BD6A651-B4E6-4D6F-B648-5A0E2EE99657"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceCollection
        {
            int GetCount(out uint pcDevices);
            int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDevice
        {
            int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
        }

        [Guid("886d8e00-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;

            public PROPERTYKEY(Guid fmtid, uint pid)
            {
                this.fmtid = fmtid;
                this.pid = pid;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pwszVal;

            public string GetString()
            {
                if (vt == 31 && pwszVal != IntPtr.Zero) // VT_LPWSTR
                {
                    return Marshal.PtrToStringUni(pwszVal);
                }
                return null;
            }
        }

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PROPVARIANT pvar);

        [Guid("f8679f50-850a-41cf-9c72-430f73f2b06c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPolicyConfigWin10
        {
            int GetGroupId();
            int GetPropertyValue();
            int SetPropertyValue();
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int role);
            int SetEndpointVisibility();
        }

        [Guid("5688034c-9d86-430f-b663-8f63027582b1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPolicyConfigVista
        {
            int GetGroupId();
            int GetPropertyValue();
            int SetPropertyValue();
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int role);
            int SetEndpointVisibility();
        }

        [Guid("8706226F-26AA-4AC2-920A-C380456E3D31"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPolicyConfig
        {
            int GetGroupId();
            int GetPropertyValue();
            int SetPropertyValue();
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int role);
            int SetEndpointVisibility();
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out int pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            int VolumeStepUp(ref Guid pguidEventContext);
            int VolumeStepDown(ref Guid pguidEventContext);
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }
    }
}
