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

        private static CoreAudioApi.IAudioEndpointVolume GetMasterVolume()
        {
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
                        return endpoint;
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
            return null;
        }

        public static float GetVolume()
        {
            CoreAudioApi.IAudioEndpointVolume endpoint = null;
            try
            {
                endpoint = GetMasterVolume();
                if (endpoint != null)
                {
                    endpoint.GetMasterVolumeLevelScalar(out float level);
                    return level;
                }
            }
            catch { }
            finally
            {
                if (endpoint != null)
                {
                    try { Marshal.ReleaseComObject(endpoint); } catch { }
                }
            }
            return 0.5f;
        }

        public static bool IsMuted()
        {
            CoreAudioApi.IAudioEndpointVolume endpoint = null;
            try
            {
                endpoint = GetMasterVolume();
                if (endpoint != null)
                {
                    endpoint.GetMute(out bool isMuted);
                    return isMuted;
                }
            }
            catch { }
            finally
            {
                if (endpoint != null)
                {
                    try { Marshal.ReleaseComObject(endpoint); } catch { }
                }
            }
            return false;
        }

        public static (float volume, bool isMuted) AdjustVolume(float deltaPercent)
        {
            CoreAudioApi.IAudioEndpointVolume endpoint = null;
            try
            {
                endpoint = GetMasterVolume();
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
            catch { }
            finally
            {
                if (endpoint != null)
                {
                    try { Marshal.ReleaseComObject(endpoint); } catch { }
                }
            }
            return (0.5f, false);
        }

        public static (float volume, bool isMuted) ToggleMute()
        {
            CoreAudioApi.IAudioEndpointVolume endpoint = null;
            try
            {
                endpoint = GetMasterVolume();
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
            catch { }
            finally
            {
                if (endpoint != null)
                {
                    try { Marshal.ReleaseComObject(endpoint); } catch { }
                }
            }
            return (0.5f, false);
        }
    }

    internal static class CoreAudioApi
    {
        public const int CLSCTX_ALL = 23;
        public const int eRender = 0;
        public const int eMultimedia = 1;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        public class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            int RegisterEndpointNotificationCallback(IntPtr pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDevice
        {
            int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
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
