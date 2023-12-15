using BLRPC.Melon;
using UnityEngine;
using UnityEngine.Rendering;
using MelonLoader;

namespace BLRPC.Internal
{
    public class QuestChecker9000
    {
        public static bool IsQest() //bonelab uses vulkan for rendering on quest, and directx11 for pc
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;

            if (deviceType == GraphicsDeviceType.Vulkan)
            {
                ModConsole.Msg("quest");
                return true; 
            }
            else if (deviceType == GraphicsDeviceType.Direct3D11)
            {
                MelonLogger.Msg("pc");
                return false;
            }
            else { return true; }
        }
    }
}
