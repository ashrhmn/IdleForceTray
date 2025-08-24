# Cursor Rules for IdleForce Tray Development

## Project Context
This is a Windows tray application written in C# using WinForms and .NET 8. The app monitors user input and forces system sleep/shutdown after idle timeout. See PRD.md for complete requirements.

## Task Management
- **CRITICAL**: Always update TODO.md when completing tasks by changing `[ ]` to `[x]`
- Mark items as completed IMMEDIATELY after finishing each task
- Never batch multiple completed tasks - update TODO.md after each individual completion
- Use the TODO.md file as the primary source of truth for development progress

## Code Style & Standards
- Follow C# naming conventions (PascalCase for classes/methods, camelCase for local variables)
- Use explicit PInvoke declarations with proper error handling
- Wrap all Win32 API calls in try-catch blocks with meaningful error messages
- Use readonly fields where possible, prefer immutable data structures
- Keep methods small and focused - single responsibility principle

## Architecture Guidelines
- Single Program.cs file is acceptable for this small utility
- Use dependency injection pattern for testable components
- Separate concerns: UI, Settings, Logging, Input Detection, Power Management
- Implement interfaces for testability (IInputDetector, ISettingsManager, etc.)

## Windows-Specific Requirements
- Target .NET 8 with Windows-specific APIs
- Use proper PInvoke signatures for Win32 APIs
- Handle missing XInput DLLs gracefully (try xinput1_4.dll, xinput1_3.dll, xinput9_1_0.dll)
- Implement proper cleanup in Dispose patterns
- Use named mutex for single instance enforcement

## Error Handling
- All PInvoke calls must be wrapped in try-catch
- Log errors using the structured logging system
- Never crash - always fail gracefully
- Return safe defaults when APIs fail
- Show user-friendly error messages in tray notifications

## Security & Privacy
- No network access whatsoever
- No telemetry or data collection
- Only request elevation when absolutely necessary (Guaranteed Sleep toggle)
- Never log actual input values - only high-level events
- Validate all settings input to prevent injection

## Performance Requirements
- CPU usage must stay under 0.2% average
- Memory usage must stay under 20 MB
- Use efficient polling intervals (5 seconds default)
- Dispose resources properly to prevent leaks
- Use timers efficiently, don't block UI thread

## Testing Guidelines
- Write unit tests for core logic (idle math, settings validation)
- Use mock interfaces for Windows APIs in tests
- Test edge cases (missing DLLs, invalid settings, etc.)
- Manual testing required for actual sleep/shutdown behavior

## File Organization
```
/
├── PRD.md (requirements)
├── TODO.md (task tracking - KEEP UPDATED!)
├── .cursorrules (this file)
├── IdleForceTray.csproj
├── Program.cs (main entry point)
├── Settings/ (if needed for complex settings)
├── Logging/ (if needed for complex logging)
└── Tests/ (unit tests)
```

## Deployment
- Target: Self-contained, single-file, trimmed executable
- Platform: win-x64 only
- Framework: .NET 8
- Output: IdleForceTray.exe (~10-15 MB)

## Development Workflow
1. Read PRD.md for requirements
2. Check TODO.md for current task
3. Implement the feature/fix
4. **IMMEDIATELY update TODO.md** to mark task complete
5. Test the implementation
6. Commit changes
7. Move to next task

## Reminder
The most important rule: **ALWAYS update TODO.md immediately after completing any task**. This is critical for tracking progress and ensuring nothing is missed.
