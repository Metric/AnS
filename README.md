# AnS
 AnS Data Downloader

# Purpose
 Downloads Auction House Data from the AnsAuctionService server.
 
# Compatability
Windows 8+, Linux, Mac

# Requirements for Any OS
- Minimum 450MB RAM (Starts at 100MB but if you use region data then it will always use about 410MB of ram when building LUA data)
	- Depending on feedback, the LUA generation may be transitioned to server side and thus only a file download will happen on client at that point
- Hard Drive Space 150MB+
- Requires x64 processor

# Known Issues
- Include Region Checkbox is not saved as a setting yet.

# Windows
See releases on the side for the latest version

# Getting it Working on Linux
- You will need to build it yourself for Linux.
- You will need Visual Studio for Linux and .Net Core 3.1 minimum + Avalonia UI 0.9.10+ Package for Visual Studio
- Remove references to System.Window.Forms
- Delete SystemTrayIcon.cs
- MainWindow.xaml.cs remove all references to SystemTrayIcon
- Use the Build -> Publish Menu Item
- Configure for linux with Release x64, Self-Contained, Produce Single File, target run time linux-x64

# Getting it Working on Mac
For the moment you will need to build it yourself for Mac, however I do plan to add in prebuilts in the near future.

- You will need Visual Studio for Mac and .Net Core 3.1 minimum + Avalonia UI 0.9.10+ Package for Visual Studio
- Remove references to System.Window.Forms
- Delete SystemTrayIcon.cs
- MainWindow.xaml.cs remove all references to SystemTrayIcon
- You will also need access to XCode to finalize the Mac build.
- Use the Build -> Publish Menu Item
- Configure for mac with Release x64, Self-Contained, Produce Single File, target run time osx-x64

