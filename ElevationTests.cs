using System;

namespace IdleForceTray.Tests;

public static class ElevationTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== Running Elevation Helper Tests ===");
        
        TestIsRunningAsAdministrator();
        TestIsHibernationEnabled();
        
        Console.WriteLine("=== All Elevation Tests Completed ===");
    }
    
    private static void TestIsRunningAsAdministrator()
    {
        Console.WriteLine("Testing IsRunningAsAdministrator()...");
        
        try
        {
            bool isAdmin = Program.IsRunningAsAdministrator();
            Console.WriteLine($"✓ IsRunningAsAdministrator() returned: {isAdmin}");
            Console.WriteLine($"  (This should be 'False' if running as regular user, 'True' if running as admin)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ IsRunningAsAdministrator() threw exception: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestIsHibernationEnabled()
    {
        Console.WriteLine("Testing IsHibernationEnabled()...");
        
        try
        {
            bool hibernationEnabled = Program.IsHibernationEnabled();
            Console.WriteLine($"✓ IsHibernationEnabled() returned: {hibernationEnabled}");
            Console.WriteLine($"  (This reflects current system hibernation state)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ IsHibernationEnabled() threw exception: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    public static void TestDisableHibernationSafely()
    {
        Console.WriteLine("=== Manual Test for DisableHibernation() ===");
        Console.WriteLine("WARNING: This test requires elevation and will modify system settings!");
        Console.WriteLine("Only run this test if you want to actually disable hibernation.");
        Console.WriteLine("Press 'Y' to continue, any other key to skip...");
        
        var key = Console.ReadKey();
        Console.WriteLine();
        
        if (key.Key == ConsoleKey.Y)
        {
            Console.WriteLine("Testing DisableHibernation()...");
            
            try
            {
                bool result = Program.DisableHibernation();
                Console.WriteLine($"✓ DisableHibernation() returned: {result}");
                if (result)
                {
                    Console.WriteLine("  Hibernation should now be disabled on this system.");
                }
                else
                {
                    Console.WriteLine("  Failed to disable hibernation (check logs for details).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ DisableHibernation() threw exception: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Skipped DisableHibernation() test (requires elevation).");
        }
        
        Console.WriteLine();
    }
}