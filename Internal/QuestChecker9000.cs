using BLRPC.Melon;
using UnityEngine;
using UnityEngine.Rendering;
using MelonLoader;

namespace BLRPC.Internal
{
    public class QuestChecker9000 //this probably wont ever be actually put into the mod as mae said that the mod isnt getting updated but i was bored and spend 2 hours on this like a psycho
    {
        public static bool IsQest() //bonelab uses vulkan for rendering on quest, and directx11 for pc
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;

            if (deviceType == GraphicsDeviceType.Vulkan)
            {
                return true; 
            }
            else if (deviceType == GraphicsDeviceType.Direct3D11)
            {
                return false;
            }
            else { return true; }
        }
    }
}
