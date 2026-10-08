using System;
using System.Threading;
using System.Threading.Tasks;
using DirectInputManager;
using Xunit;

namespace DirectInputManager.Tests
{
    /// <summary>Pure managed logic that must hold without a device or the native DLL.</summary>
    public class ManagerLogicTests
    {
        [Fact]
        public void FlatJoyState2_equality_is_field_exact()
        {
            var a = new FlatJoyState2 { lX = 1, lFRz = 65535, buttonsB = 1UL << 63, rgdwPOV = 0x8001 };
            var b = a;
            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            b.lFRz = 65534;             // a field the cheap hash does not cover
            Assert.False(a.Equals(b));  // equality must still see it

            b = a; b.buttonsB = 0;
            Assert.False(a.Equals(b));
            Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Attach_rejects_unknown_guid_without_touching_native()
        {
            // No device was enumerated in this process, so the guid cannot be known.
            bool attached = DIManager.Attach("{00000000-0000-0000-0000-000000000000}");
            Assert.False(attached);
            Assert.Equal(unchecked((int)0x80070490), DIManager.LastHResult);
            Assert.Contains("EnumerateDevices", DIManager.LastError);
        }

        [Fact]
        public void UpdateCustomForceNative_rejects_empty_sample_array()
        {
            Assert.False(DIManager.UpdateCustomForceNative("{00000000-0000-0000-0000-000000000000}", Array.Empty<int>(), 0));
            Assert.Equal(unchecked((int)0x80070057), DIManager.LastHResult);
        }

        [Fact]
        public void Describe_names_directinput_codes_and_falls_back_for_others()
        {
            Assert.Contains("DIERR_NOTEXCLUSIVEACQUIRED", WinErrors.Describe(unchecked((int)0x80040205)));
            Assert.Contains("DIERR_INPUTLOST", WinErrors.Describe(unchecked((int)0x8007001E)));
            Assert.StartsWith("0x80040205", WinErrors.Describe(unchecked((int)0x80040205)));
        }

        [Fact]
        public async Task Debouncer_runs_once_after_quiet_period_without_a_synchronization_context()
        {
            // Native device-change callbacks may arrive on a thread with no SynchronizationContext.
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var debouncer = new Debouncer(50);
                int runs = 0;
                var done = new TaskCompletionSource<bool>();
                for (int i = 0; i < 5; i++) debouncer.Debounce(() => { Interlocked.Increment(ref runs); done.TrySetResult(true); });
                await Task.WhenAny(done.Task, Task.Delay(2000));
                await Task.Delay(100); // give any stray continuation a chance to run
                Assert.Equal(1, runs);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }
    }
}
