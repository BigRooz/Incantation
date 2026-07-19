using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Exposes the friendly name of the current Windows default audio rendering endpoint.
/// This component is display-only and never changes Unity or operating-system audio routing.
/// </summary>
public sealed class WindowsAudioOutputDeviceProvider : MonoBehaviour
{
    public const string FallbackDeviceName = "System Default";

    public event Action DeviceNameChanged;

    public string CurrentDeviceName { get; private set; } = FallbackDeviceName;

    private void OnEnable()
    {
        AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
        Refresh();
    }

    private void OnDisable()
    {
        AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
    }

    public void Refresh()
    {
        string detectedName = TryGetDefaultOutputDeviceName();
        string nextName = string.IsNullOrWhiteSpace(detectedName)
            ? FallbackDeviceName
            : detectedName;

        if (string.Equals(CurrentDeviceName, nextName, StringComparison.Ordinal))
        {
            return;
        }

        CurrentDeviceName = nextName;
        DeviceNameChanged?.Invoke();
    }

    private void HandleAudioConfigurationChanged(bool deviceWasChanged)
    {
        Refresh();
    }

    private static string TryGetDefaultOutputDeviceName()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        IMMDeviceEnumerator enumerator = null;
        IMMDevice device = null;
        IPropertyStore propertyStore = null;
        PropVariant value = default;

        try
        {
            Type enumeratorType = Type.GetTypeFromCLSID(MMDeviceEnumeratorClassId, true);
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);
            int result = enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            if (result != 0 || device == null)
            {
                return FallbackDeviceName;
            }

            result = device.OpenPropertyStore(StorageAccessMode.Read, out propertyStore);
            if (result != 0 || propertyStore == null)
            {
                return FallbackDeviceName;
            }

            PropertyKey friendlyNameKey = PropertyKey.DeviceFriendlyName;
            result = propertyStore.GetValue(ref friendlyNameKey, out value);
            if (result != 0 || value.ValueType != VariantType.StringPointer)
            {
                return FallbackDeviceName;
            }

            return Marshal.PtrToStringUni(value.PointerValue);
        }
        catch
        {
            return FallbackDeviceName;
        }
        finally
        {
            if (value.HasValue)
            {
                PropVariantClear(ref value);
            }
            ReleaseComObject(propertyStore);
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
#else
        return FallbackDeviceName;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static readonly Guid MMDeviceEnumeratorClassId =
        new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static void ReleaseComObject(object comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant propVariant);

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    [Flags]
    private enum StorageAccessMode
    {
        Read = 0
    }

    private enum VariantType : ushort
    {
        StringPointer = 31
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        private Guid formatId;
        private uint propertyId;

        public static PropertyKey DeviceFriendlyName => new PropertyKey
        {
            formatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            propertyId = 14
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] private VariantType valueType;
        [FieldOffset(8)] private IntPtr pointerValue;

        public VariantType ValueType => valueType;
        public IntPtr PointerValue => pointerValue;
        public bool HasValue => (ushort)valueType != 0;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParameters, out IntPtr interfacePointer);
        [PreserveSig] int OpenPropertyStore(StorageAccessMode accessMode, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
#endif
}
