﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MyVaultBrowser;

namespace InvAddIn
{
    internal class Win32Api
    {
        private const string DIALOG_CLASS_NAME = "#32770";

        private const uint EVENT_OBJECT_CREATE = 0x8000;

        private const uint EVENT_OBJECT_DESTROY = 0x8001;

        private const uint WINEVENT_OUTOFCONTEXT = 0;

        // Need to ensure delegate is not collected while we're using it,
        // storing it in a class field is simplest way to do this.
        private static readonly WinEventDelegate procDelegate = WinEventProc;

        private static IntPtr hhook = IntPtr.Zero;
        private static uint hview = 0;

        /// <summary>
        /// Call win32 procedure to listen to the creation of the vault browser.
        /// </summary>
        /// <param name="idProcess">The process id of Inventor.</param>
        /// <param name="idThread">The main window thread of Inventor, optional.</param>
        public static void SetEventHook(uint view, uint idProcess, uint idThread = 0)
        {
            // In case the previous hook is not unhooked.
            if (hhook != IntPtr.Zero)
                UnHookEvent();

            hview = view;

            // Listen for object create in inventor process.
            hhook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE, IntPtr.Zero,
                procDelegate, idProcess, idThread, WINEVENT_OUTOFCONTEXT);
        }

        /// <summary>
        /// Call win32 procedure to stop listening.
        /// </summary>
        public static void UnHookEvent()
        {
            UnhookWinEvent(hhook);

            //set it to Zero again, indicating there is no hook.
            hhook = IntPtr.Zero;
            hview = 0;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr
            hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess,
            uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // filter out non-window objects... (eg. items within a listbox)
            if (idObject != 0 || idChild != 0)
            {
                return;
            }

            // Find a window with class name #32770 (dialog), in which our vault browser lives.
            var stringBuilder = new StringBuilder(256);
            var ret = GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);

            if (ret > 0 && stringBuilder.ToString() == DIALOG_CLASS_NAME)
            {
                // Find the parent window and check the title of it,
                // if it is Vault, then we are done.
                var pHwnd = GetParent(hwnd);
                ret = GetWindowText(pHwnd, stringBuilder, stringBuilder.Capacity);

                if (ret > 0 && stringBuilder.ToString() == "Vault")
                {
                    StandardAddInServer.AddVaultBrowserHwnd((int) hview, pHwnd);
                    UnHookEvent();
                    
#if DEBUG
                    Debug.WriteLine($"Vault Browser: {(int)pHwnd:X}");
#endif
                }
            }
        }

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    }
}