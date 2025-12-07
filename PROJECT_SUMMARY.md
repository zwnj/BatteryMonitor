# Project Summary: BatteryMonitor3

This document provides an overview of the BatteryMonitor3 project, its current implementation, and the primary unresolved issue.

## 1. Project Overview

- **Purpose**: A WPF desktop application that monitors and displays detailed battery status.
- **UI Paradigm**: The application runs primarily from the system tray (notification area). A window with detailed battery information is shown upon user interaction with the tray icon.
- **Architecture**: The project follows the MVVM (Model-View-ViewModel) pattern.
  - **View**: `PopupMovedWindow.xaml` (a borderless `Window`)
  - **ViewModel**: `BatteryViewModel.cs`
  - **Service**: `BatteryService.cs` (fetches battery data using WMI).
- **Key Technologies**:
  - .NET 8
  - WPF (Windows Presentation Foundation)
  - `Hardcodet.Wpf.TaskbarNotification` for the system tray icon functionality.

## 2. Current Implementation: The "Window" Approach

The user-facing UI is a custom, borderless `Window` that has replaced an earlier implementation that used a simple `Popup`.

### Key Features:
- **Movable Window**: The user can drag the window by its header to any position on the screen.
- **Position Persistence**: The application saves the window's last position and restores it on the next launch.
- **Dual Interaction Modes**:
  1.  **Click-to-Pin (Sticky Mode)**: A left-click on the tray icon shows the window and "pins" it. In this mode, the window stays visible until the user clicks the tray icon again or clicks anywhere outside the window.
  2.  **Hover-to-Show**: Hovering the mouse over the tray icon for a short duration shows the same window temporarily.

## 3. The Core Unresolved Issue

The primary problem lies in the "Hover-to-Show" mode's stability.

-   **The Problem**: When the mouse cursor is moved over the tray icon and then held **static** (without moving), the window appears correctly but then closes after approximately 1.5 seconds. Immediately after closing, it re-opens, creating a distracting "flickering" or "blinking" loop.

-   **The Cause**:
    1.  The application currently determines if the mouse is "over the tray icon" by checking a timestamp (`_lastActivityTime`) that is only updated when the `TrayMouseMove` event fires.
    2.  When the mouse stops moving, `TrayMouseMove` no longer fires, and the timestamp becomes "stale".
    3.  A watchdog timer, running every few hundred milliseconds, checks this timestamp. After 1.5 seconds, it concludes that the mouse is no longer over the icon (because the timestamp is old) and hides the window.
    4.  The act of the `Window` disappearing causes the OS to re-evaluate the UI underneath the cursor. This immediately triggers a new `TrayMouseMove` event, which updates the timestamp and restarts the entire show/hide cycle.

-   **The Challenge**: The root cause is the difficulty in reliably detecting if a **static** mouse cursor is over the tray icon. The `Hardcodet.NotifyIcon.Wpf` library does not provide a simple property (`IsMouseOver`) or event (`TrayMouseLeave`) for this. A robust solution would require complex system-level (P/Invoke) calls to get the icon's screen coordinates, which have been difficult to implement correctly within this context. The current implementation is a best-effort heuristic that suffers from this flickering side effect.

**Next Step**: The goal for the next session is to resolve this flickering issue, likely by pursuing an alternative approach such as making a `Popup` control draggable, which may avoid the OS-level eventing issues seen with using a full `Window`.
