using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DirectInputManager
{
    class Native
    {
        // Windows x64 only. Unity ships the DLL from Runtime/Plugins/DLL; the Explorer csproj copies it next to the exe.
        const string DLLFile = @"DirectInputForceFeedback.dll";
        [DllImport(DLLFile, CharSet = CharSet.Ansi, EntryPoint = "UpdateConstantForce")]
        internal static extern int UpdateConstantForceSimple([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int magnitude);

        [DllImport(DLLFile)]
        internal static extern void InitializeForStandalone();

        [DllImport(DLLFile)]
        internal static extern int StartDirectInput();

        [DllImport(DLLFile)]
        internal static extern int StopDirectInput();

        [DllImport(DLLFile)]
        internal static extern IntPtr EnumerateDevices(out int deviceCount);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int CreateDevice([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DestroyDevice([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceState([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out FlatJoyState2 DeviceState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceStateRaw([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out DIJOYSTATE2 DeviceState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceCapabilities([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out DIDEVCAPS DeviceCapabilitiesOut);


        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int EnumerateFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out int count, out IntPtr strings);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int EnumerateFFBAxes([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out int count, out IntPtr strings);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int CreateFFBEffect([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DestroyFFBEffect([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateConstantForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int magnitude);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdatePeriodicForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType, int magnitude, uint period, int offset);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateRampForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int startMagnitude, int endMagnitude, uint durationMicroseconds);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateConditionForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType,
            int offset, int positiveCoefficient, int negativeCoefficient,
            uint positiveSaturation, uint negativeSaturation, int deadBand);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateCustomForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance,
            [In] int[] forceData, int sampleCount, uint samplePeriod);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int StopAllFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int SetAutocenter([MarshalAs(UnmanagedType.LPStr)] string guidInstance, [MarshalAs(UnmanagedType.I1)] bool autocenterState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetFFBEffectStatus([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType, out uint status);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetSupportedFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out uint mask);


        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DEBUG1([MarshalAs(UnmanagedType.LPStr)] string guidInstance,
            [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] DEBUGDATA);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning restore IDE0079 // Remove unnecessary suppression

        [DllImport(DLLFile)]
        internal static extern int GetActiveDevices(out int count, out IntPtr strings);

        [DllImport(DLLFile)]
        internal static extern void FreeStringArray(IntPtr strings, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        [DllImport(DLLFile)]
        internal static extern void SetDeviceChangeCallback([MarshalAs(UnmanagedType.FunctionPtr)] DeviceChangeCallback onDeviceChange);


    }
    public class DIManager
    {
        // Define all callback delegates at class level with proper attributes
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        // Static delegate instances to prevent garbage collection
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0052 // Remove unread private members
        private static Native.DeviceChangeCallback s_deviceChangeCallback;
#pragma warning restore IDE0052 // Remove unread private members
#pragma warning restore IDE0079 // Remove unnecessary suppression


        //////////////////////////////////////////////////////////////
        // Cross Platform "Macros" - Allows lib to work in Visual Studio & Unity
        //////////////////////////////////////////////////////////////
        internal static bool showLogsRuntime = false; // read by the Unity on-screen logger path only

#if UNITY_5_3_OR_NEWER
        private static uint ClampAgnostic(uint value, uint min, uint max) => (uint)Mathf.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Mathf.Clamp(value, min, max);

        private static void DebugLog(string message)
        {
            // Send to console too
            UnityEngine.Debug.Log($"<color=#9416f9>[DirectInputManager]</color> <color=#ff0000>{message}</color>");
            if (Application.isPlaying && showLogsRuntime)
            {
                DirectInputLogger.AddLog(message);
            }

        }
#else
        private static uint ClampAgnostic(uint value, uint min, uint max) => Math.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Math.Clamp(value, min, max);
        private static void DebugLog(string message) => System.Diagnostics.Debug.WriteLine($"[DirectInputManager]{message}");
#endif

        //////////////////////////////////////////////////////////////
        // Private Variables - For Internal use
        //////////////////////////////////////////////////////////////

        private static bool _isInitialized = false;               // is DIManager ready
        private static DeviceInfo[] _devices = Array.Empty<DeviceInfo>(); // Hold data for devices plugged in
        private static readonly Dictionary<string, ActiveDeviceInfo> _activeDevices = new(); // Hold data for devices actively attached

        //////////////////////////////////////////////////////////////
        // Public Variables
        //////////////////////////////////////////////////////////////
        public static bool IsInitialized { get => _isInitialized; }
        public static DeviceInfo[] Devices { get => _devices; }
        public static Dictionary<string, ActiveDeviceInfo> ActiveDevices { get => _activeDevices; }

        //////////////////////////////////////////////////////////////
        // Methods
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes DirectInput<br/><br/>
        /// </summary>
        /// <returns>
        /// True if sucessful or DI already initialized<br/>
        /// False if failed
        /// </returns>
        public static bool Initialize()
        {
            if (_isInitialized) { return _isInitialized; }
            try
            {
#if !UNITY_5_3_OR_NEWER
                Native.InitializeForStandalone();
#endif
                if (Native.StartDirectInput() != 0) { return _isInitialized = false; }
                s_deviceChangeCallback = OnDeviceChange;
                Native.SetDeviceChangeCallback(s_deviceChangeCallback);
                // The DLL outlives a Unity domain reload; a pointer to a collected delegate would crash on the next hot-plug.
                AppDomain.CurrentDomain.DomainUnload += (sender, args) => Native.SetDeviceChangeCallback(null);
                return _isInitialized = true;
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to initialize DirectInput: {ex.Message}");
                return _isInitialized = false;
            }
        }

        /// <summary>
        /// Fetch currently available devices and populate DIManager.devices<br/>
        /// </summary>
        public static void EnumerateDevices()
        {
            IntPtr ptrDevices = Native.EnumerateDevices(out int deviceCount); // Returns pointer to list of devices and how many are available

            if (deviceCount > 0)
            {
                _devices = new DeviceInfo[deviceCount];

                int deviceSize = Marshal.SizeOf(typeof(DeviceInfo)); // Size of each Device entry
                for (int i = 0; i < deviceCount; i++)
                {
                    IntPtr pCurrent = ptrDevices + i * deviceSize; // Ptr to the current device
                    _devices[i] = Marshal.PtrToStructure<DeviceInfo>(pCurrent); // Transform the Ptr into a C# instance of DeviceInfo
                }
            }
            else
            {
                _devices = Array.Empty<DeviceInfo>(); // empty _devices when no devices are present
            }
            return;
        }

        public static async Task EnumerateDevicesAsync()
        {
            Task enumDevicesTask = Task.Run(EnumerateDevices);
            Task warningTimeout = Task.Delay(1000);

            if (warningTimeout == await Task.WhenAny(enumDevicesTask, warningTimeout))
            {
                DebugLog($"Warning EnumerateDevices is taking longer than expected!");
                await enumDevicesTask; // Continue to wait for EnumerateDevices
            }
        }

        /// <summary>
        /// Attach to Device, ready to get state/ForceFeedback<br/><br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Device was attached
        /// </returns>
        public static bool Attach(string guidInstance)
        {
            if (_activeDevices.ContainsKey(guidInstance)) { return true; } // We're already attached to that device
            int index = Array.FindIndex(_devices, d => d.guidInstance == guidInstance);
            if (index < 0)
            {
                LastHResult = unchecked((int)0x80070490); // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)
                LastError = $"Attach: device {guidInstance} is not in the enumerated device list; call EnumerateDevices first";
                DebugLog(LastError);
                return false;
            }
            if (!Check("CreateDevice", Native.CreateDevice(guidInstance), guidInstance)) { return false; }
            _activeDevices.Add(guidInstance, new ActiveDeviceInfo() { deviceInfo = _devices[index] }); // Add device to our C# active device tracker (Dictionary allows us to easily check if GUID already exists)
            return true;
        }


        /// <summary>
        /// Remove a specified Device
        /// </summary>
        /// <returns>
        /// True upon sucessful destruction or device already didn't exist
        /// </returns>
        public static bool Destroy(string guidInstance)
        {
            if (!_activeDevices.ContainsKey(guidInstance)) { return true; } // We don't think we're attached to that device, consider it removed
            int hresult = Native.DestroyDevice(guidInstance);
            _activeDevices.Remove(guidInstance); // remove from our C# active device tracker
            return Check("DestroyDevice", hresult, guidInstance);
        }

        /// <summary>
        /// Retrieve state of the Device, Flattened for easier comparison.<br/>
        /// </summary>
        /// <returns>
        /// FlatJoyState2
        /// </returns>
        public static FlatJoyState2 GetDeviceState(string guidInstance)
        {
            int hresult = Native.GetDeviceState(guidInstance, out FlatJoyState2 DeviceState);
            if (hresult != 0) { DebugLog($"GetDeviceState Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
            return DeviceState;
        }

        // public static async Task<FlatJoyState2> GetDeviceStateAsync(string guidInstance){
        //   return await Task.Run(()=>{return GetDeviceState(guidInstance);});
        // }

        /// <summary>
        /// Retrieve state of the Device<br/>
        /// *Warning* DIJOYSTATE2 contains arrays making it difficult to compare, concider using GetDeviceState
        /// </summary>
        /// <returns>
        /// DIJOYSTATE2
        /// </returns>
        public static DIJOYSTATE2 GetDeviceStateRaw(string guidInstance)
        {
            int hresult = Native.GetDeviceStateRaw(guidInstance, out DIJOYSTATE2 DeviceState);
            if (hresult != 0) { DebugLog($"GetDeviceStateRaw Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
            return DeviceState;
        }

        public static string[] GetActiveDevices()
        {
            int hr = Native.GetActiveDevices(out int count, out IntPtr stringsPtr);

            if (FAILED(hr) || count == 0 || stringsPtr == IntPtr.Zero)
            {
                DebugLog($"GetActiveDevices Failed: 0x{hr:x} {WinErrors.GetSystemMessage(hr)}");
                return Array.Empty<string>();
            }

            try
            {
                string[] result = ReadStringArray(stringsPtr, count);
                if (result.Length != _activeDevices.Count)
                {
                    DebugLog($"Active Device mismatch! DLL:{result.Length}, DIManager:{_activeDevices.Count}");
                }
                return result;
            }
            finally
            {
                Native.FreeStringArray(stringsPtr, count);
            }
        }

        public static string[] GetDeviceFFBCapabilities(string guidInstance)
        {
            int hr = Native.EnumerateFFBEffects(guidInstance, out int count, out IntPtr stringsPtr);

            if (FAILED(hr) || count == 0 || stringsPtr == IntPtr.Zero)
            {
                DebugLog($"GetDeviceFFBCapabilities Failed: 0x{hr:x} {WinErrors.GetSystemMessage(hr)}");
                return Array.Empty<string>();
            }

            try
            {
                return ReadStringArray(stringsPtr, count);
            }
            finally
            {
                Native.FreeStringArray(stringsPtr, count);
            }
        }

        private static string[] ReadStringArray(IntPtr stringsPtr, int count)
        {
            string[] result = new string[count];
            for (int i = 0; i < count; ++i)
            {
                IntPtr currentPtr = Marshal.ReadIntPtr(stringsPtr, i * IntPtr.Size);
                result[i] = DirectInputManager.WinErrors.PtrToStringUTF8(currentPtr);
            }
            return result;
        }

        // Helper method to check HRESULT
        private static bool FAILED(int hr)
        {
            return hr < 0;
        }

        /// <summary>
        /// HRESULT of the most recent native call made through a DIManager method that reports
        /// success or failure (0 on success). Lets a caller show the actual API error instead of a bare false.
        /// </summary>
        /// <summary>The DLL returns E_ABORT for "effect not created" and "effect already exists".</summary>
        private const int E_ABORT = unchecked((int)0x80004004);
        public static int LastHResult { get; private set; }

        /// <summary>
        /// Human readable description of the most recent failure reported by a DIManager method, or empty.
        /// </summary>
        public static string LastError { get; private set; } = "";

        // Records the outcome of a native call. Logs failures unless the caller is a per-frame update path.
        // HRESULT success is "not negative": DirectInput returns DI_DOWNLOADSKIPPED (3), DI_EFFECTRESTARTED (4)
        // and DI_TRUNCATED (8) for successful calls, so a "!= 0" test would report them as failures.
        private static bool Check(string operation, int hresult, string detail = "", bool log = true)
        {
            LastHResult = hresult;
            if (hresult >= 0) { LastError = ""; return true; }
            LastError = $"{operation} failed: {WinErrors.Describe(hresult)} {detail}".TrimEnd();
            if (log) { DebugLog(LastError); }
            return false;
        }

        /// <summary>
        /// Retrieve the capabilities of the device. E.g. # Buttons, # Axes, Driver Version<br/>
        /// Device must be attached first<br/>
        /// </summary>
        /// <returns>
        /// DIDEVCAPS https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
        /// </returns>
        public static DIDEVCAPS GetDeviceCapabilities(string guidInstance)
        {
            int hresult = Native.GetDeviceCapabilities(guidInstance, out DIDEVCAPS DeviceCapabilities);
            if (hresult != 0) { DebugLog($"GetDeviceCapabilities Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
            return DeviceCapabilities;
        }

        /// <summary>
        /// Returns if the device has the ForceFeedback Flag<br/>
        /// </summary>
        /// <returns>
        /// True if device can provide ForceFeedback<br/>
        /// </returns>
        public static bool FFBCapable(string guidInstance)
        {
            return GetDeviceCapabilities(guidInstance).dwFlags.HasFlag(DwFlags.DIDC_FORCEFEEDBACK);
        }

        /// <summary>
        /// Returns the attached status of the device<br/>
        /// </summary>
        /// <returns>
        /// True if device is attached
        /// </returns>
        public static bool IsDeviceActive(string guidInstance)
        {
            return _activeDevices.ContainsKey(guidInstance);
        }

        /// <summary>
        /// Enables an FFB Effect on the device.<br/>
        /// E.g. FFBEffects.ConstantForce<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was added sucessfully
        /// </returns>
        public static bool EnableFFBEffect(string guidInstance, FFBEffects effectType)
        {
            return Check("CreateFFBEffect", Native.CreateFFBEffect(guidInstance, effectType), effectType.ToString());
        }

        /// <summary>
        /// Removes an FFB Effect from the device<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was removed sucessfully
        /// </returns>
        public static bool DestroyFFBEffect(string guidInstance, FFBEffects effectType)
        {
            return Check("DestroyFFBEffect", Native.DestroyFFBEffect(guidInstance, effectType), effectType.ToString());
        }

        /// <summary>
        /// Stops and removes all active effects on a device<br/>
        /// *Warning* Effects will have to be enabled again before use<br/>
        /// </summary>
        /// <returns>
        /// True if effects stopped successfully
        /// </returns>
        public static bool StopAllFFBEffects(string guidInstance)
        {
            return Check("StopAllFFBEffects", Native.StopAllFFBEffects(guidInstance), guidInstance);
        }

        /// <summary>
        /// Turns the device's own centering spring on or off (DIPROP_AUTOCENTER). Many wheels enable it by
        /// default, where it fights the effects an application plays. The DLL briefly unacquires the device
        /// to change the property and restarts active effects afterwards.
        /// </summary>
        public static bool SetAutocenter(string guidInstance, bool enabled)
        {
            return Check("SetAutocenter", Native.SetAutocenter(guidInstance, enabled), guidInstance);
        }

        /// <summary>
        /// Which standard effect types the attached device enumerates: bit n set means FFBEffects value n
        /// is available. Returns false, with LastError set, when the device is not attached or when the
        /// native DLL predates this export (then treat every effect as possibly supported).
        /// </summary>
        public static bool TryGetSupportedFFBEffects(string guidInstance, out uint mask)
        {
            mask = 0;
            try
            {
                return Check("GetSupportedFFBEffects", Native.GetSupportedFFBEffects(guidInstance, out mask), guidInstance);
            }
            catch (EntryPointNotFoundException)
            {
                LastHResult = unchecked((int)0x80004001); // E_NOTIMPL
                LastError = "GetSupportedFFBEffects: the loaded DirectInputForceFeedback.dll predates this export; rebuild it from src~/DirectInputForceFeedback~";
                return false;
            }
        }

        /// <summary>
        /// Asks DirectInput whether an effect is playing right now (IDirectInputEffect::GetEffectStatus).<br/>
        /// Returns false, with LastError set, when the effect was never created on this device or when the
        /// native DLL predates this export.
        /// </summary>
        public static bool TryGetFFBEffectStatus(string guidInstance, FFBEffects effectType, out bool playing)
        {
            playing = false;
            try
            {
                if (!Check("GetFFBEffectStatus", Native.GetFFBEffectStatus(guidInstance, effectType, out uint status), effectType.ToString(), log: false)) { return false; }
                playing = (status & 1) != 0; // DIEGES_PLAYING
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                LastHResult = unchecked((int)0x80004001); // E_NOTIMPL
                LastError = "GetFFBEffectStatus: the loaded DirectInputForceFeedback.dll predates this export; rebuild it from src~/DirectInputForceFeedback~";
                return false;
            }
        }

        /// <summary>
        /// Stops DirectInput<br/>
        /// </summary>
        /// <returns>
        /// True if DirectInput was stopped successfully
        /// </returns>
        public static bool StopDirectInput()
        {
            return Check("StopDirectInput", Native.StopDirectInput());
        }

        /// <summary>
        /// Returns byte[] of state<br/>
        /// </summary>
        /// <returns>
        /// byte[]
        /// </returns>
        public static byte[] FlatStateToBytes(FlatJoyState2 state)
        {
            int size = Marshal.SizeOf(state);
            byte[] StateRawBytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(state, ptr, true);
            Marshal.Copy(ptr, StateRawBytes, 0, size);
            Marshal.FreeHGlobal(ptr);
            return StateRawBytes;
        }

        /// <summary>
        /// Computes MD5 for FlatState<br/>
        /// </summary>
        /// <returns>
        /// byte[] MD5 Hash
        /// </returns>
        public static byte[] FlatStateMD5(FlatJoyState2 state)
        {
            using MD5 md5 = MD5.Create();
            var StateRawBytes = FlatStateToBytes(state);
            return md5.ComputeHash(StateRawBytes);
        }

        /// <summary>
        /// Fetches device state and triggers events if state changed<br/>
        /// </summary>
        public static void Poll(string guidInstance)
        {
            if (_activeDevices.TryGetValue(guidInstance, out ActiveDeviceInfo ADI))
            { // Check if device active
                // A failed read (device unplugged, input lost) leaves the struct zeroed; publishing it would report full-lock axes.
                if (!Check("GetDeviceState", Native.GetDeviceState(guidInstance, out FlatJoyState2 state), guidInstance, log: false)) { return; }
                if (!ADI.hasState || !state.Equals(ADI.lastState)) // Field-wise compare: no allocation, no hashing per poll
                {
                    ADI.lastState = state;
                    ADI.hasState = true;
                    ADI.stateHash = state.GetHashCode();
                    ADI.DeviceStateChange(ADI.deviceInfo, state); // Invoke all event listeners for this device
                }
            }
            else
            {
                // Device isn't attached
            }

        }

        /// <summary>
        /// Fetches device state for all devices and queues events<br/>
        /// </summary>
        public static void PollAll()
        {
            foreach (ActiveDeviceInfo ADI in _activeDevices.Values)
            {
                Poll(ADI.deviceInfo);
            }
        }

        /// <summary>
        /// Obtains ActiveDeviceInfo for specified GUID<br/>
        /// </summary>
        /// <returns>
        /// Bool if GUID was found <br/>
        /// OUT ADI of device if found
        /// </returns>
        public static bool GetADI(string guidInstance, out ActiveDeviceInfo ADI)
        {
            return _activeDevices.TryGetValue(guidInstance, out ADI);
        }

        /// <summary>
        /// *Internal use only*
        /// Used to test C++ code in the DLL during devlopment
        /// </summary>
        // public static string[] DEBUG1() {
        //   string[] DEBUGDATA = null;
        //   DEBUGDATA = new string[1] { "Test" };
        //   int hresult = Native.DEBUG1(out DEBUGDATA);
        //   if (hresult != 0) { DebugLog($"DEBUG1 Failed: 0x{hresult.ToString("x")} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }

        //   return DEBUGDATA;
        // }


        //////////////////////////////////////////////////////////////
        // Device Events
        //////////////////////////////////////////////////////////////

        // Events to add listners too                   E.g. DIManager.OnDeviceAdded += MyFunctionWhenDeviceAdded;
        public static event deviceInfoEvent OnDeviceAdded;
        public static event deviceInfoEvent OnDeviceRemoved;

        // Functions to invoke event listeners
        public static void DeviceAdded(DeviceInfo device) { OnDeviceAdded?.Invoke(device); }
        public static void DeviceRemoved(DeviceInfo device) { OnDeviceRemoved?.Invoke(device); }

        // static Action InvokeDebounce;

        private static readonly Debouncer ODCDebouncer = new Debouncer(150);        // 150ms (OnDeviceChangeDebouncer)

        /// <summary>
        /// *Internal use only*
        /// Called from the DLL when a windows WM_DEVICECHANGE event is captured
        /// This function invokes the necessary events
        /// </summary>
#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(Native.DeviceChangeCallback))]
#endif
        private static void OnDeviceChange(DBTEvents DBTEvent)
        {
            //DebugLog($"DeviceChange {DBTEvent.ToString()}");

            ODCDebouncer.Debounce(() => { ScanDevicesForChanges(); });
        }

        private static async void ScanDevicesForChanges()
        {
            DeviceInfo[] oldDevices = _devices;                              // Store currently known devices
            try { await EnumerateDevicesAsync(); }                           // Fetch what devices are available now
            catch (Exception ex) { DebugLog($"ScanDevicesForChanges: enumeration failed: {ex.Message}"); return; }

            var removedDevices = oldDevices.Except(_devices);
            var addedDevices = _devices.Except(oldDevices);

            foreach (DeviceInfo device in removedDevices)
            {                  // Process removed devices
                if (_activeDevices.TryGetValue(device.guidInstance, out ActiveDeviceInfo ADI))
                {
                    ADI.DeviceRemoved(device);                                   // Invoke event listeners for this device
                }
                DeviceRemoved(device);                                         // Invoke all event listeners all devices
                Destroy(device);                                               // If device was connceted remove it gracefully
                                                                               // DebugLog($"{device.productName} Removed!");
            }

            foreach (DeviceInfo device in addedDevices)
            {                    // Process newly added devices
                DeviceAdded(device);                                           // Invoke event to broadcast a new device is available
            }
        }

        //////////////////////////////////////////////////////////////
        // Effect Specific Methods
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Update existing effect with new DICONDITION array<br/><br/>
        ///
        /// DICondition[DeviceFFBEffectAxesCount]:<br/><br/>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/><br/>
        /// Only element 0 reaches the DLL, which applies it to every actuator axis. For non-condition effects the
        /// fields are reused: ConstantForce magnitude = positiveCoefficient; RampForce start/end = positive/negativeCoefficient;
        /// periodic magnitude = positiveCoefficient, period (microseconds, unclamped) = positiveSaturation, offset = offset;
        /// CustomForce samples = positiveCoefficient of every element, sample period (unclamped) = element 0 negativeCoefficient.
        /// Prefer the typed Update*Native functions.
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateEffect(string guidInstance, FFBEffects effectType, DICondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
            {
                DebugLog("UpdateEffect: Invalid conditions array");
                return false;
            }
            uint period = (uint)Math.Max(0, conditions[0].positiveSaturation);
            uint samplePeriod = (uint)Math.Max(0, conditions[0].negativeCoefficient);
            conditions = (DICondition[])conditions.Clone(); // clamp a copy, not the caller's array

            // Apply clamping to all values
            for (int i = 0; i < conditions.Length; i++)
            {
                conditions[i].deadband = ClampAgnostic(conditions[i].deadband, 0, 10000);
                conditions[i].offset = ClampAgnostic(conditions[i].offset, -10000, 10000);
                conditions[i].negativeCoefficient = ClampAgnostic(conditions[i].negativeCoefficient, -10000, 10000);
                conditions[i].positiveCoefficient = ClampAgnostic(conditions[i].positiveCoefficient, -10000, 10000);
                conditions[i].negativeSaturation = ClampAgnostic(conditions[i].negativeSaturation, 0, 10000);
                conditions[i].positiveSaturation = ClampAgnostic(conditions[i].positiveSaturation, 0, 10000);
            }

            // Use the appropriate native function based on effect type
            bool success = false;
            switch (effectType)
            {
                case FFBEffects.ConstantForce:
                    success = UpdateConstantForceNative(guidInstance, conditions[0].positiveCoefficient);
                    break;

                case FFBEffects.Spring:
                case FFBEffects.Damper:
                case FFBEffects.Friction:
                case FFBEffects.Inertia:
                    success = UpdateConditionForceNative(guidInstance, effectType,
                        conditions[0].offset,
                        conditions[0].positiveCoefficient,
                        conditions[0].negativeCoefficient,
                        (uint)conditions[0].positiveSaturation,
                        (uint)conditions[0].negativeSaturation,
                        conditions[0].deadband);
                    break;

                case FFBEffects.RampForce:
                    success = UpdateRampForceNative(guidInstance,
                        conditions[0].positiveCoefficient,
                        conditions[0].negativeCoefficient);
                    break;

                case FFBEffects.Sine:
                case FFBEffects.Square:
                case FFBEffects.Triangle:
                case FFBEffects.SawtoothUp:
                case FFBEffects.SawtoothDown:
                    success = UpdatePeriodicForceNative(guidInstance, effectType,
                        conditions[0].positiveCoefficient,
                        period,
                        conditions[0].offset);
                    break;

                case FFBEffects.CustomForce:
                    // For custom force, we extract the data from conditions array
                    int[] forceData = new int[conditions.Length];
                    for (int i = 0; i < conditions.Length; i++)
                    {
                        forceData[i] = conditions[i].positiveCoefficient;
                    }
                    success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
                    break;

                default:
                    DebugLog($"UpdateEffect: Unsupported effect type {effectType}");
                    return false;
            }

            if (!success)
            {
                DebugLog($"UpdateEffect: Failed to update {effectType}");
            }

            return success;
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateConstantForceSimple(string guidInstance, int magnitude)
        {
            return UpdateConstantForceNative(guidInstance, magnitude);
        }

        /// <summary>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateSpringSimple(string guidInstance, uint deadband, int offset, int negativeCoefficient, int positiveCoefficient, uint negativeSaturation, uint positiveSaturation)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Spring,
                offset, positiveCoefficient, negativeCoefficient,
                positiveSaturation, negativeSaturation, (int)deadband);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateDamperSimple(string guidInstance, int magnitude)
        {
            // Saturation is the maximum force the condition may output (0..10000); 0 would silence the effect
            // on devices that honour DIDC_SATURATION, so the simple helpers always allow full output.
            return UpdateConditionForceNative(guidInstance, FFBEffects.Damper,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateFrictionSimple(string guidInstance, int magnitude)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Friction,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateInertiaSimple(string guidInstance, int magnitude)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Inertia,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        public static bool UpdatePeriodicSimple(string guidInstance, FFBEffects effectType, int magnitude, uint period = 30000, int rampStart = 0, int rampEnd = 0)
        {
            // For RampForce effect, use UpdateRampForceNative
            if (effectType == FFBEffects.RampForce)
            {
                bool success = UpdateRampForceNative(guidInstance, rampStart, rampEnd);
                if (!success && LastHResult == E_ABORT && EnableFFBEffect(guidInstance, effectType)) // effect not created yet
                {
                    success = UpdateRampForceNative(guidInstance, rampStart, rampEnd);
                }
                return success;
            }
            // For other periodic effects, use UpdatePeriodicForceNative
            else
            {
                bool success = UpdatePeriodicForceNative(guidInstance, effectType, magnitude, period, 0);
                if (!success && LastHResult == E_ABORT && EnableFFBEffect(guidInstance, effectType)) // effect not created yet
                {
                    success = UpdatePeriodicForceNative(guidInstance, effectType, magnitude, period, 0);
                }
                return success;
            }
        }

        public static bool UpdateCustomForceSimple(string guidInstance, int[] forceData, uint samplePeriod, int offset = 0, uint deadband = 0)
        {
            if (forceData == null)
            {
                DebugLog("UpdateCustomForceSimple: Invalid input parameters");
                return false;
            }

            bool success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
            if (!success && LastHResult == E_ABORT && EnableFFBEffect(guidInstance, FFBEffects.CustomForce)) // effect not created yet
            {
                success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
            }
            return success;
        }

        //////////////////////////////////////////////////////////////
        // Overloads                                                //
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Attach to Device, ready to get state/ForceFeedback<br/><br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Device was attached
        /// </returns>
        public static bool Attach(DeviceInfo device) => Attach(device.guidInstance);

        /// <summary>
        /// Remove a specified Device
        /// </summary>
        /// <returns>
        /// True upon sucessful destruction
        /// </returns>
        public static bool Destroy(DeviceInfo device) => Destroy(device.guidInstance);

        /// <summary>
        /// Retrieve state of the Device, Flattened for easier comparison.<br/>
        /// </summary>
        /// <returns>
        /// FlatJoyState2
        /// </returns>
        public static FlatJoyState2 GetDeviceState(DeviceInfo device) => GetDeviceState(device.guidInstance);

        /// <summary>
        /// Retrieve state of the Device<br/>
        /// *Warning* DIJOYSTATE2 contains arrays making it difficult to compare, concider using GetDeviceState
        /// </summary>
        /// <returns>
        /// DIJOYSTATE2
        /// </returns>
        public static DIJOYSTATE2 GetDeviceStateRaw(DeviceInfo device) => GetDeviceStateRaw(device.guidInstance);

        /// <summary>
        /// Retrieve the capabilities of the device. E.g. # Buttons, # Axes, Driver Version<br/>
        /// Device must be attached first<br/>
        /// </summary>
        /// <returns>
        /// DIDEVCAPS https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
        /// </returns>
        public static DIDEVCAPS GetDeviceCapabilities(DeviceInfo device) => GetDeviceCapabilities(device.guidInstance);

        /// <summary>
        /// Returns if the device has the ForceFeedback Flag<br/>
        /// </summary>
        /// <returns>
        /// True if device can provide ForceFeedback<br/>
        /// </returns>
        public static bool FFBCapable(DeviceInfo device) => FFBCapable(device.guidInstance);

        /// <summary>
        /// Returns the attached status of the device<br/>
        /// </summary>
        /// <returns>
        /// True if device is attached
        /// </returns>
        public static bool IsDeviceActive(DeviceInfo device) => IsDeviceActive(device.guidInstance);

        /// <summary>
        /// Enables an FFB Effect on the device.<br/>
        /// E.g. FFBEffects.ConstantForce<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was added sucessfully
        /// </returns>
        public static bool EnableFFBEffect(DeviceInfo device, FFBEffects effectType) => EnableFFBEffect(device.guidInstance, effectType);

        /// <summary>
        /// Removes an FFB Effect from the device<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was removed sucessfully
        /// </returns>
        public static bool DestroyFFBEffect(DeviceInfo device, FFBEffects effectType) => DestroyFFBEffect(device.guidInstance, effectType);

        /// <summary>
        /// Fetches supported FFB Effects by specified Device<br/>
        /// </summary>
        /// <returns>
        /// string[] of effect names supported
        /// </returns>
        public static string[] GetDeviceFFBCapabilities(DeviceInfo device) => GetDeviceFFBCapabilities(device.guidInstance);

        /// <summary>
        /// Stops and removes all active effects on a device<br/>
        /// *Warning* Effects will have to be enabled again before use<br/>
        /// </summary>
        /// <returns>
        /// True if effects stopped successfully
        /// </returns>
        public static bool StopAllFFBEffects(DeviceInfo device) => StopAllFFBEffects(device.guidInstance);

        /// <summary>
        /// Fetches device state and triggers events if state changed<br/>
        /// </summary>
        public static void Poll(DeviceInfo device) => Poll(device.guidInstance);

        /// <summary>
        /// Obtains ActiveDeviceInfo for specified GUID<br/>
        /// </summary>
        /// <returns>
        /// Bool if GUID was found <br/>
        /// OUT ADI of device if found
        /// </returns>
        public static bool GetADI(DeviceInfo device, out ActiveDeviceInfo ADI) => GetADI(device.guidInstance, out ADI);

        //////////////////////////////////////////////////////////////
        // Effect Specific Methods Overloads
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Update existing effect with new DICONDITION array<br/><br/>
        ///
        /// DICondition[DeviceFFBEffectAxesCount]:<br/><br/>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateEffect(DeviceInfo device, FFBEffects effect, DICondition[] conditions) => UpdateEffect(device.guidInstance, effect, conditions);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateConstantForceSimple(DeviceInfo device, int Magnitude) => UpdateConstantForceSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateSpringSimple(DeviceInfo device, uint deadband, int offset, int negativeCoefficient, int positiveCoefficient, uint negativeSaturation, uint positiveSaturation) => UpdateSpringSimple(device.guidInstance, deadband, offset, negativeCoefficient, positiveCoefficient, negativeSaturation, positiveSaturation);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateDamperSimple(DeviceInfo device, int Magnitude) => UpdateDamperSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateFrictionSimple(DeviceInfo device, int Magnitude) => UpdateFrictionSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateInertiaSimple(DeviceInfo device, int Magnitude) => UpdateInertiaSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Updates a periodic force feedback effect with specified magnitude
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing if the Effect updated successfully
        /// </returns>
        public static bool UpdatePeriodicSimple(DeviceInfo device, FFBEffects effectType, int Magnitude, uint period = 30000, int rampStart = 0, int rampEnd = 0) => UpdatePeriodicSimple(device.guidInstance, effectType, Magnitude, period, rampStart, rampEnd);

        public static bool UpdateCustomForceEffect(DeviceInfo device, int[] forceData, uint samplePeriod) => UpdateCustomForceSimple(device.guidInstance, forceData, samplePeriod);

        /// <summary>
        /// Updates the constant force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="magnitude">Force [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateConstantForceNative(string guidInstance, int magnitude)
        {
            return Check("UpdateConstantForce", Native.UpdateConstantForce(guidInstance, magnitude), log: false);
        }

        /// <summary>
        /// Updates a periodic effect (Sine, Square, Triangle, etc.).
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="effectType">Type of periodic effect</param>
        /// <param name="magnitude">Effect amplitude [0, 10000] - note: unsigned, use offset for direction</param>
        /// <param name="period">Period in microseconds</param>
        /// <param name="offset">Effect offset [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdatePeriodicForceNative(string guidInstance, FFBEffects effectType, int magnitude, uint period = 30000, int offset = 0)
        {
            int positiveMagnitude = Math.Abs(magnitude);
            if (positiveMagnitude > 10000) positiveMagnitude = 10000;

            return Check("UpdatePeriodicForce", Native.UpdatePeriodicForce(guidInstance, effectType, positiveMagnitude, period, offset), log: false);
        }

        /// <summary>
        /// Updates a ramp force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="startMagnitude">Starting force [-10000, 10000]</param>
        /// <param name="endMagnitude">Ending force [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        /// <summary>Plays a ramp from startMagnitude to endMagnitude once over durationMicroseconds (a ramp cannot run forever).</summary>
        public static bool UpdateRampForceNative(string guidInstance, int startMagnitude, int endMagnitude, uint durationMicroseconds = 1000000)
        {
            return Check("UpdateRampForce", Native.UpdateRampForce(guidInstance, startMagnitude, endMagnitude, durationMicroseconds), log: false);
        }

        /// <summary>
        /// Updates a condition effect (Spring, Damper, Friction, Inertia).
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="effectType">Type of condition effect</param>
        /// <param name="offset">Center offset [-10000, 10000]</param>
        /// <param name="positiveCoefficient">Positive side coefficient [-10000, 10000]</param>
        /// <param name="negativeCoefficient">Negative side coefficient [-10000, 10000]</param>
        /// <param name="positiveSaturation">Positive side saturation [0, 10000]</param>
        /// <param name="negativeSaturation">Negative side saturation [0, 10000]</param>
        /// <param name="deadBand">Dead zone [0, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateConditionForceNative(string guidInstance, FFBEffects effectType,
            int offset = 0, int positiveCoefficient = 10000, int negativeCoefficient = 10000,
            uint positiveSaturation = 10000, uint negativeSaturation = 10000, int deadBand = 0)
        {
            return Check("UpdateConditionForce", Native.UpdateConditionForce(guidInstance, effectType,
                offset, positiveCoefficient, negativeCoefficient,
                positiveSaturation, negativeSaturation, deadBand), log: false);
        }

        /// <summary>
        /// Updates a custom force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="forceData">Array of force values [-10000, 10000]</param>
        /// <param name="samplePeriod">Sample period in microseconds</param>
        /// <returns>True if the update was successful</returns>
        /// <summary>Downloads forceData (-10000..10000 each) as the custom force samples; samplePeriod is microseconds, 0 keeps the period the DLL took from DIDEVCAPS.</summary>
        public static bool UpdateCustomForceNative(string guidInstance, int[] forceData, uint samplePeriod)
        {
            if (forceData == null || forceData.Length == 0) { LastHResult = unchecked((int)0x80070057); LastError = "UpdateCustomForce: forceData must contain at least one sample"; return false; }
            return Check("UpdateCustomForce", Native.UpdateCustomForce(guidInstance, forceData, forceData.Length, samplePeriod), log: false);
        }

    } // End of DIManager



    //////////////////////////////////////////////////////////////
    // Utilities
    //////////////////////////////////////////////////////////////

    /// <summary>
    /// Helper class to print out user friendly system error codes.
    /// Taken from: https://stackoverflow.com/a/21174331/9053848
    /// </summary>
    public static class WinErrors
    {
        #region definitions
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int FormatMessage(FormatMessageFlags dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);

        [Flags]
        private enum FormatMessageFlags : uint
        {
            FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100,
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,
            FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
            FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000,
            FORMAT_MESSAGE_FROM_HMODULE = 0x00000800,
            FORMAT_MESSAGE_FROM_STRING = 0x00000400,
        }
        #endregion

        /// <summary>
        /// Gets a user friendly string message for a system error code
        /// </summary>
        /// <param name="errorCode">System error code</param>
        /// <returns>Error string</returns>
        public static string GetSystemMessage(int errorCode)
        {
            try
            {
                IntPtr lpMsgBuf = IntPtr.Zero;

                int dwChars = FormatMessage(
                    FormatMessageFlags.FORMAT_MESSAGE_ALLOCATE_BUFFER | FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM | FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero,
                    (uint)errorCode,
                    0, // Default language
                    ref lpMsgBuf,
                    0,
                    IntPtr.Zero);
                if (dwChars == 0)
                {
                    // Handle the error.
                    int le = Marshal.GetLastWin32Error();
                    return "Unable to get error code string from System - Error " + le.ToString();
                }

                string sRet = PtrToStringUTF8(lpMsgBuf);

                // Free the buffer.
                lpMsgBuf = LocalFree(lpMsgBuf);
                return sRet;
            }
            catch (Exception e)
            {
                return "Unable to get error code string from System -> " + e.ToString();
            }
        }

        /// <summary>
        /// Describes an HRESULT: DirectInput codes by name (FormatMessage does not know them), then the system message.
        /// </summary>
        public static string Describe(int hr)
        {
            string name = hr switch
            {
                0 => "DI_OK",
                1 => "S_FALSE (DI_NOEFFECT / DI_NOTATTACHED / DI_PROPNOEFFECT)",
                3 => "DI_DOWNLOADSKIPPED",
                4 => "DI_EFFECTRESTARTED",
                8 => "DI_TRUNCATED",
                unchecked((int)0x80004001) => "DIERR_UNSUPPORTED (E_NOTIMPL)",
                unchecked((int)0x80004003) => "E_POINTER",
                unchecked((int)0x80004004) => "E_ABORT (DLL: effect already exists or was never created)",
                unchecked((int)0x80004005) => "DIERR_GENERIC (E_FAIL; DLL: device not attached or DirectInput not started)",
                unchecked((int)0x80070002) => "DIERR_NOTFOUND / DIERR_OBJECTNOTFOUND",
                unchecked((int)0x80070005) => "DIERR_OTHERAPPHASPRIO / DIERR_READONLY / DIERR_HANDLEEXISTS (E_ACCESSDENIED)",
                unchecked((int)0x80070006) => "E_HANDLE",
                unchecked((int)0x8007000C) => "DIERR_NOTACQUIRED",
                unchecked((int)0x80070015) => "DIERR_NOTINITIALIZED",
                unchecked((int)0x8007001E) => "DIERR_INPUTLOST",
                unchecked((int)0x80070057) => "DIERR_INVALIDPARAM (E_INVALIDARG)",
                unchecked((int)0x800700AA) => "DIERR_ACQUIRED",
                unchecked((int)0x80070490) => "ERROR_NOT_FOUND",
                unchecked((int)0x80040200) => "DIERR_INSUFFICIENTPRIVS",
                unchecked((int)0x80040201) => "DIERR_DEVICEFULL",
                unchecked((int)0x80040202) => "DIERR_MOREDATA",
                unchecked((int)0x80040203) => "DIERR_NOTDOWNLOADED",
                unchecked((int)0x80040204) => "DIERR_HASEFFECTS",
                unchecked((int)0x80040205) => "DIERR_NOTEXCLUSIVEACQUIRED",
                unchecked((int)0x80040206) => "DIERR_INCOMPLETEEFFECT",
                unchecked((int)0x80040207) => "DIERR_NOTBUFFERED",
                unchecked((int)0x80040208) => "DIERR_EFFECTPLAYING",
                unchecked((int)0x80040209) => "DIERR_UNPLUGGED",
                unchecked((int)0x8004020A) => "DIERR_REPORTFULL",
                unchecked((int)0x8004F300) => "DIERR_NOWINDOWHANDLE (DLL could not create its hidden window)",
                unchecked((int)0x8004F301) => "DIERR_UNITYNOTINITIALIZED (plugin load callback has not run)",
                _ => null,
            };
            string text = name != null ? name : GetSystemMessage(hr).Trim();
            return $"0x{hr:x8} {text}";
        }

        public static string PtrToStringUTF8(IntPtr ptr) //by Risto-Paasivirta
        {
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) ++len;
            if (len == 0) return "";
            byte[] array = new byte[len];
            Marshal.Copy(ptr, array, 0, len);
            return Encoding.UTF8.GetString(array);
        }
    }

    /// <summary>
    /// Only execute an Action after it hasn't been called for a timeout period <br/>
    /// Setup: private static Debouncer DebouncerName = new Debouncer(300); // 300ms<br/>
    /// Invocation: DebouncerName.Debounce(() => { Console.WriteLine("Executed"); });<br/>
    /// Source: https://stackoverflow.com/a/47933557/3055031 (Modifed)
    /// </summary>
    public class Debouncer
    {
        private List<CancellationTokenSource> CancelTokens;
        private readonly int TimeoutMs;
        private readonly object _lockThis;                    // Use a locking object to prevent the debouncer to trigger again while the func is still running

        public Debouncer(int timeoutMs = 300)
        {
            this.TimeoutMs = timeoutMs;
            CancelTokens = new List<CancellationTokenSource>();
            _lockThis = new object();
        }

        public void Debounce(Action TargetAction)
        {
            CancelAllTokens();                                                 // Cancel existing Tokens Each invocation
            var tokenSource = new CancellationTokenSource();                   // Token for this invocation
            lock (_lockThis) { CancelTokens.Add(tokenSource); }                // Safely add this Token to the list
            // Run the action on the caller's synchronization context (Unity main thread, WinForms UI thread).
            // A thread without one (plain console host) must not throw inside a native callback, so fall back to the pool.
            TaskScheduler scheduler = SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Default;
            Task.Delay(TimeoutMs, tokenSource.Token).ContinueWith(task =>
            {    // (Note: All Tasks continue)
                if (!tokenSource.IsCancellationRequested)
                {                      // if this is the task that hasn't been canceled
                    CancelAllTokens();                                             // Clear
                    CancelTokens = new List<CancellationTokenSource>();            // Empty List
                    lock (_lockThis) { TargetAction(); }                           // Excute Action
                }
            }, scheduler);
        }

        private void CancelAllTokens()
        {
            lock (_lockThis)
            {
                foreach (var token in CancelTokens)
                {
                    if (!token.IsCancellationRequested) { token.Cancel(); }
                }
            }
        }
    }

    /// <summary>
    /// Enum of possible FFB Effects<br/>
    /// </summary>
    public enum FFBEffects
    {
        ConstantForce = 0,
        RampForce = 1,
        Square = 2,
        Sine = 3,
        Triangle = 4,
        SawtoothUp = 5,
        SawtoothDown = 6,
        Spring = 7,
        Damper = 8,
        Inertia = 9,
        Friction = 10,
        CustomForce = 11
    }

    public struct CustomForceData
    {
        public int[] ForceData;      // Array of force values
        public uint SamplePeriod;    // Time in microseconds between samples
        public uint Channels;        // Number of channels (typically 1)
    }

    /// <summary>
    /// Types of OnDeviceChange DBTEvents<br/>
    /// More info: https://docs.microsoft.com/en-us/windows/win32/devio/wm-devicechange
    /// </summary>
    public enum DBTEvents
    {
        DBT_DEVNODES_CHANGED = 0x0007,
        DBT_QUERYCHANGECONFIG = 0x0017,
        DBT_CONFIGCHANGED = 0x0018,
        DBT_CONFIGCHANGECANCELED = 0x0019,
        DBT_DEVICEARRIVAL = 0x8000,
        DBT_DEVICEQUERYREMOVE = 0x8001,
        DBT_DEVICEQUERYREMOVEFAILED = 0x8002,
        DBT_DEVICEREMOVEPENDING = 0x8003,
        DBT_DEVICEREMOVECOMPLETE = 0x8004,
        DBT_DEVICETYPESPECIFIC = 0x8005,
        DBT_CUSTOMEVENT = 0x8006,
        DBT_USERDEFINED = 0xFFFF
    }

    /// <summary>
    /// Struct to hold device specific info<br/>
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfo
    {
        public uint deviceType;
        [MarshalAs(UnmanagedType.LPStr)] public string guidInstance;
        [MarshalAs(UnmanagedType.LPStr)] public string guidProduct;
        [MarshalAs(UnmanagedType.LPStr)] public string instanceName;
        [MarshalAs(UnmanagedType.LPStr)] public string productName;
        [MarshalAs(UnmanagedType.I1)] public bool FFBCapable; // native field is a one-byte C++ bool, not a four-byte BOOL
    }

    public delegate void deviceInfoEvent(DeviceInfo device); // delegate for handling events that pass DeviceInfo
    public delegate void deviceStateEvent(DeviceInfo device, FlatJoyState2 state); // delegate for handling events that pass DeviceInfo & FlatJoyState2
    /// <summary>
    /// Like DeviceInfo but allows for events per device<br/>
    /// </summary>
    public class ActiveDeviceInfo
    {
        public DeviceInfo deviceInfo;                     // Hold the info about the device
        public Int32 stateHash;                           // Hash of the last known state (see FlatJoyState2.GetHashCode)
        public FlatJoyState2 lastState;                   // Last state delivered through OnDeviceStateChange
        public bool hasState;                             // False until the first poll has delivered a state
        public event deviceInfoEvent OnDeviceRemoved;     // Event to add listners too
        public event deviceStateEvent OnDeviceStateChange; // Event to add listners too
        public void DeviceRemoved(DeviceInfo device) { OnDeviceRemoved?.Invoke(device); }         // Function to invoke event listeners
        public void DeviceStateChange(DeviceInfo device, FlatJoyState2 state) { OnDeviceStateChange?.Invoke(device, state); } // Function to invoke event listeners
    }

    /// <summary>
    /// DirectInput DIConditon Struct <br/>
    /// Offset: -10,000 - 10,000 <br/>
    /// positiveCoefficient: -10,000 - 10,000 <br/>
    /// negativeCoefficient: -10,000 - 10,000 <br/>
    /// positiveSaturation: 0 - 10,000 <br/>
    /// negativeSaturation: 0 - 10,000 <br/>
    /// deadband: 0 - 10,000 <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416601(v=vs.85)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DICondition
    {
        /// <summary>
        /// Offset for the condition, in the range from - 10,000 through 10,000.
        /// </summary>
        public int offset;
        /// <summary>
        /// Coefficient constant on the positive side of the offset, in the range
        /// from - 10,000 through 10,000.
        /// </summary>
        public int positiveCoefficient;
        /// <summary>
        /// Coefficient constant on the negative side of the offset, in the range
        /// from - 10,000 through 10,000. If the device does not support separate
        /// positive and negative coefficients, the value of lNegativeCoefficient
        /// is ignored, and the value of lPositiveCoefficient is used as both the
        /// positive and negative coefficients.
        /// </summary>
        public int negativeCoefficient;
        /// <summary>
        /// Maximum force output on the positive side of the offset, in the range
        /// from 0 through 10,000.
        ///
        /// If the device does not support force saturation, the value of this
        /// member is ignored.
        /// </summary>
        public uint positiveSaturation;
        /// <summary>
        /// Maximum force output on the negative side of the offset, in the range
        /// from 0 through 10,000.
        ///
        /// If the device does not support force saturation, the value of this member
        /// is ignored.
        ///
        /// If the device does not support separate positive and negative saturation,
        /// the value of dwNegativeSaturation is ignored, and the value of dwPositiveSaturation
        /// is used as both the positive and negative saturation.
        /// </summary>
        public uint negativeSaturation;
        /// <summary>
        /// Range about the center of the axis that is ignored by the effect.
        /// This value is in the range from 0 through 10,000.
        /// </summary>
        public int deadband;  // Changed from uint to int to match native LONG type
    }

    /// <summary>
    /// Describes the state of a joystick device with extended capabilities. <br/>
    /// See https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416628(v=vs.85)
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DIJOYSTATE2
    {
        public int lX;
        public int lY;
        public int lZ;
        public int lRx;
        public int lRy;
        public int lRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglSlider;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public int[] rgdwPOV;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] rgbButtons;
        public int lVX;
        public int lVY;
        public int lVZ;
        public int lVRx;
        public int lVRy;
        public int lVRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglVSlider;
        public int lAX;
        public int lAY;
        public int lAZ;
        public int lARx;
        public int lARy;
        public int lARz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglASlider;
        public int lFX;
        public int lFY;
        public int lFZ;
        public int lFRx;
        public int lFRy;
        public int lFRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglFSlider;
    }

    /// <summary>
    /// A flattend version of DIJOYSTATE2 without nested arrays<br/>
    /// See https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416628(v=vs.85)
    /// </summary>
    [Serializable]
    public struct FlatJoyState2 : IEquatable<FlatJoyState2>
    { // Axis trimmed from "LONG" UInt32 to UInt16 as values range 0-65535?
        public UInt64 buttonsA; // Buttons seperated into banks of 64-Bits to fit into Unsigned 64-bit integer
        public UInt64 buttonsB; // Buttons seperated into banks of 64-Bits to fit into Unsigned 64-bit integer
        public UInt16 lX;       // X-axis
        public UInt16 lY;       // Y-axis
        public UInt16 lZ;       // Z-axis
        public UInt16 lU;       // U-axis
        public UInt16 lV;       // V-axis
        public UInt16 lRx;      // X-axis rotation
        public UInt16 lRy;      // Y-axis rotation
        public UInt16 lRz;      // Z-axis rotation
        public UInt16 lVX;      // X-axis velocity
        public UInt16 lVY;      // Y-axis velocity
        public UInt16 lVZ;      // Z-axis velocity
        public UInt16 lVU;      // U-axis velocity
        public UInt16 lVV;      // V-axis velocity
        public UInt16 lVRx;     // X-axis angular velocity
        public UInt16 lVRy;     // Y-axis angular velocity
        public UInt16 lVRz;     // Z-axis angular velocity
        public UInt16 lAX;      // X-axis acceleration
        public UInt16 lAY;      // Y-axis acceleration
        public UInt16 lAZ;      // Z-axis acceleration
        public UInt16 lAU;      // U-axis acceleration
        public UInt16 lAV;      // V-axis acceleration
        public UInt16 lARx;     // X-axis angular acceleration
        public UInt16 lARy;     // Y-axis angular acceleration
        public UInt16 lARz;     // Z-axis angular acceleration
        public UInt16 lFX;      // X-axis force
        public UInt16 lFY;      // Y-axis force
        public UInt16 lFZ;      // Z-axis force
        public UInt16 lFU;      // U-axis force
        public UInt16 lFV;      // V-axis force
        public UInt16 lFRx;     // X-axis torque
        public UInt16 lFRy;     // Y-axis torque
        public UInt16 lFRz;     // Z-axis torque
        public UInt16 rgdwPOV;  // Store each DPAD in chunks of 4 bits inside 16-bit UInt

        /// <summary>Exact comparison of every field. This is what DIManager.Poll uses to detect a change.</summary>
        public readonly bool Equals(FlatJoyState2 o) =>
            buttonsA == o.buttonsA && buttonsB == o.buttonsB &&
            lX == o.lX && lY == o.lY && lZ == o.lZ && lU == o.lU && lV == o.lV &&
            lRx == o.lRx && lRy == o.lRy && lRz == o.lRz &&
            lVX == o.lVX && lVY == o.lVY && lVZ == o.lVZ && lVU == o.lVU && lVV == o.lVV &&
            lVRx == o.lVRx && lVRy == o.lVRy && lVRz == o.lVRz &&
            lAX == o.lAX && lAY == o.lAY && lAZ == o.lAZ && lAU == o.lAU && lAV == o.lAV &&
            lARx == o.lARx && lARy == o.lARy && lARz == o.lARz &&
            lFX == o.lFX && lFY == o.lFY && lFZ == o.lFZ && lFU == o.lFU && lFV == o.lFV &&
            lFRx == o.lFRx && lFRy == o.lFRy && lFRz == o.lFRz &&
            rgdwPOV == o.rgdwPOV;

        public override readonly bool Equals(object obj) => obj is FlatJoyState2 o && Equals(o);

        /// <summary>
        /// Cheap hash over buttons, the eight primary axes and the POVs. Not a full-state digest:
        /// use Equals for exact comparison, or DIManager.FlatStateMD5 for a digest.
        /// </summary>
        public override readonly int GetHashCode()
        {
            unchecked
            {
                ulong h = buttonsA * 0x9E3779B97F4A7C15UL ^ buttonsB;
                h = h * 31 + ((ulong)lX | (ulong)lY << 16 | (ulong)lZ << 32 | (ulong)lRz << 48);
                h = h * 31 + ((ulong)lU | (ulong)lV << 16 | (ulong)lRx << 32 | (ulong)lRy << 48);
                h = h * 31 + rgdwPOV;
                return (int)h ^ (int)(h >> 32);
            }
        }
    }

    /// <summary>
    /// Describes a DirectInput device's capabilities. <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
    /// </summary>
    [Serializable]
    public struct DIDEVCAPS
    {
        public UInt32 dwSize;                // Size of this structure, in bytes.
        public DwFlags dwFlags;               // Flags associated with the device.
        public UInt32 dwDevType;             // Device type specifier. The least-significant byte of the device type description code specifies the device type. The next-significant byte specifies the device subtype. This value can also be combined with DIDEVTYPE_HID, which specifies a Human Interface Device (human interface device).
        public UInt32 dwAxes;                // Number of axes available on the device.
        public UInt32 dwButtons;             // Number of buttons available on the device.
        public UInt32 dwPOVs;                // Number of point-of-view controllers available on the device.
        public UInt32 dwFFSamplePeriod;      // Minimum time between playback of consecutive raw force commands, in microseconds.
        public UInt32 dwFFMinTimeResolution; // Minimum time, in microseconds, that the device can resolve. The device rounds any times to the nearest supported increment. For example, if the value of dwFFMinTimeResolution is 1000, the device would round any times to the nearest millisecond.
        public UInt32 dwFirmwareRevision;    // Firmware revision of the device.
        public UInt32 dwHardwareRevision;    // Hardware revision of the device.
        public UInt32 dwFFDriverVersion;     // Version number of the device driver.
    }

    /// <summary>
    /// Describes the Flags associated with a DirectInput device's capabilities. <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)#:~:text=IDirectInputDevice8%3A%3AGetCapabilities%20method.-,dwFlags,-Flags%20associated%20with
    /// </summary>
    [Flags]
    public enum DwFlags
    {
        DIDC_ALIAS = 0x00010000, // The device is a duplicate of another DirectInput device.
        DIDC_ATTACHED = 0x00000001, // The device is physically attached to the user's computer.
        DIDC_DEADBAND = 0x00004000, // The device supports deadband for at least one force-feedback condition.
        DIDC_EMULATED = 0x00000004, // If this flag is set, the data is coming from a user mode device interface, such as a Human Interface Device (human interface device), or by some other ring 3 means. If it is not set, the data is coming directly from a kernel mode driver.
        DIDC_FORCEFEEDBACK = 0x00000100, // The device supports force feedback.
        DIDC_FFFADE = 0x00000400, // The force-feedback system supports the fade parameter for at least one effect.
        DIDC_FFATTACK = 0x00000200, // The force-feedback system supports the attack parameter for at least one effect.
        DIDC_HIDDEN = 0x00040000, // Fictitious device created by a device driver so that it can generate keyboard and mouse events.
        DIDC_PHANTOM = 0x00020000, // Placeholder. Phantom devices are by default not enumerated.
        DIDC_POLLEDDATAFORMAT = 0x00000008, // At least one object in the current data format is polled, rather than interrupt-driven.
        DIDC_POLLEDDEVICE = 0x00000002, // At least one object on the device is polled, rather than interrupt-driven. HID devices can contain a mixture of polled and nonpolled objects.
        DIDC_POSNEGCOEFFICIENTS = 0x00001000, // The force-feedback system supports two coefficient values for conditions (one for the positive displacement of the axis and one for the negative displacement of the axis) for at least one condition. If the device does not support both coefficients, the negative coefficient in the DICONDITION structure is ignored.
        DIDC_POSNEGSATURATION = 0x00002000, // The force-feedback system supports a maximum saturation for both positive and negative force output for at least one condition. If the device does not support both saturation values, the negative saturation in the DICONDITION structure is ignored.
        DIDC_SATURATION = 0x00000800, // The force-feedback system supports the saturation of condition effects for at least one condition. If the device does not support saturation, the force generated by a condition is limited only by the maximum force that the device can generate.
        DIDC_STARTDELAY = 0x00008000, // The force-feedback system supports the start delay parameter for at least one effect. If the device does not support start delays, the dwStartDelay member of the DIEFFECT structure is ignored.
    }
}
