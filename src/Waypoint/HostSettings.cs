using Launcher.Core;

namespace Waypoint;

/// <summary>
/// The settings a window hosted by another executable reads: 流放之路服务端.exe and 流放之路登录器.exe host Waypoint's sections
/// from a different assembly, so <see cref="Shared"/>'s cast of <c>Application.Current</c> to this assembly's <c>App</c>
/// would throw. A host sets these before it creates the window; when Waypoint.exe starts itself nothing is set and the
/// window reads its own App's settings.
/// </summary>
static class HostSettings
{
    internal static Settings? Current { get; set; }
}
