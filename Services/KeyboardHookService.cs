using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace BatteryMonitor3.Services.Keyboard
{
    public class KeyboardHookService : IDisposable
    {
        // イベント定義
        public event EventHandler TriggerActivated;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        
        // 右Shiftキーの仮想キーコード (VK_RSHIFT)
        private const int VK_RSHIFT = 0xA1;

        // 設定
        private const int REQUIRED_PRESS_COUNT = 2;
        private const int TIMEOUT_MS = 400;
        private const int ACTIVATION_COOLDOWN_MS = 500;

        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        // 状態管理
        private int _pressCount = 0;
        private DateTime _lastPressTime = DateTime.MinValue;
        private DateTime _lastActivationTime = DateTime.MinValue;
        private bool _isRightShiftDown = false;

        public KeyboardHookService()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_RSHIFT)
                {
                    if (!_isRightShiftDown)
                    {
                        _isRightShiftDown = true;
                        OnRightShiftPressed();
                    }
                }
                else
                {
                    // 右Shift以外のキーが押されたらリセット
                    ResetCount();
                }
            }
            else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_RSHIFT)
                {
                    _isRightShiftDown = false;
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void OnRightShiftPressed()
        {
            DateTime now = DateTime.Now;

            if ((now - _lastActivationTime).TotalMilliseconds < ACTIVATION_COOLDOWN_MS)
            {
                ResetCount();
                return;
            }
            
            // 前回の押下から時間が空きすぎていたらリセット
            if ((now - _lastPressTime).TotalMilliseconds > TIMEOUT_MS)
            {
                _pressCount = 1;
            }
            else
            {
                _pressCount++;
            }

            _lastPressTime = now;

            if (_pressCount >= REQUIRED_PRESS_COUNT)
            {
                // トリガー発動
                _lastActivationTime = now;
                TriggerActivated?.Invoke(this, EventArgs.Empty);
                ResetCount();
            }
        }

        private void ResetCount()
        {
            _pressCount = 0;
            _lastPressTime = DateTime.MinValue;
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        // --- P/Invoke 定義 ---
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
