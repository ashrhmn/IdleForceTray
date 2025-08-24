# IdleForce Tray Development TODO

## Project Setup
- [x] Create PRD.md, TODO.md, and .cursorrules files
- [x] Create WinForms project named IdleForceTray on .NET 8

## Core Implementation
- [x] Add PInvoke for GetLastInputInfo, SetSuspendState, XInputGetState with three DLL names
- [x] Implement named mutex for single instance
- [x] Implement tray NotifyIcon with context menu and event handlers
- [x] Implement settings persistence with JSON
- [ ] Implement startup toggle using ShellLink with Task Scheduler fallback
- [ ] Implement logging with rolling files
- [ ] Implement timer loop with try/catch protection
- [ ] Implement elevation prompt helper for Guaranteed Sleep toggle

## Testing
- [ ] Write unit tests for idle math and settings

## Build & Deployment
- [ ] Provide publish profile for single file self-contained win-x64, trim true

## Instructions for AI Assistant
Please mark items as completed in this TODO.md file every time you finish a task by changing `[ ]` to `[x]`. This helps track progress and ensures nothing is missed.

When starting a new task, please:
1. Mark the current task as `[x]` completed
2. Update this file to reflect the current state
3. Continue to the next task

This TODO list corresponds to the development checklist in section 16 of the PRD.md file.
