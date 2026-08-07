# VirtualDriveManager

**VirtualDriveManager** is a native WPF (Windows Presentation Foundation) desktop application for Windows 11 designed to easily mount and unmount ISO, IMG, and other disk image files. It provides a clean, dedicated user interface that bypasses the default Windows File Explorer mounter while utilizing Windows' underlying native mounting engine.

---

[![IMAGE ALT TEXT](https://github.com/galadam96/VirtualDriveManager/blob/master/IMG/1.png)



## 🚀 Key Features

- **Mount Disk Images:** Easily mount `.iso` and `.img` image files as virtual drives.
- **Unmount Drives:** Safely eject and unmount active virtual drives with a single click.
- **Live Status List:** View all currently mounted image files alongside their assigned drive letters (e.g., `E:`, `F:`) and full file paths in a structured overview.
- **Native Engine:** Leverages the built-in Windows disk image management APIs for maximum system compatibility and stability.
- **Administrator Elevation:** Automatically handles the required administrative permissions for disk operations.

---

## 🛠️ Tech Stack

- **Language / Framework:** C# / .NET (WPF - Windows Presentation Foundation)
- **IDE:** Visual Studio (Solution / C# Project)
- **License:** GNU General Public License v3.0 (GPL-3.0)

---

## 📋 Prerequisites & Installation

### Prerequisites
- **Operating System:** Windows 10 / Windows 11
- **Runtime:** .NET Desktop Runtime (or Visual Studio with C# workload for building from source)

### Building and Running from Source
1. Clone the repository:
   ```bash
   git clone [https://github.com/](https://github.com/)<your-username>/VirtualDriveManager.git
