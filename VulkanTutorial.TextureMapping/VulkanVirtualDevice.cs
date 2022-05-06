using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanVirtualDevice : VulkanDependancy, IDisposable
{
    private readonly Device device;
    public Device Device => this.device;
    private readonly Queue graphicsQueue;
    public Queue GraphicsQueue => this.graphicsQueue;
    private readonly Queue presentQueue;
    public Queue PresentQueue => this.presentQueue;

    public VulkanVirtualDevice(Vk vk, VulkanInstance vulkanInstance, VulkanPhysicalDevice vulkanPhysicalDevice, string[] deviceExtensions) : base(vk)
    {
        var (graphicsFamily, presentFamily) = vulkanPhysicalDevice.FindQueueFamilies();
        var uniqueQueueFamilies = graphicsFamily == presentFamily
            ? new[] { graphicsFamily!.Value }
            : new[] { graphicsFamily!.Value, presentFamily!.Value };

        unsafe
        {

            using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
            var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

            var queuePriority = 1f;
            for (var i = 0; i < uniqueQueueFamilies.Length; i++)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = uniqueQueueFamilies[i],
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
                queueCreateInfos[i] = queueCreateInfo;
            }

            var deviceFeatures = new PhysicalDeviceFeatures();

            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
                PQueueCreateInfos = queueCreateInfos,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = (uint)deviceExtensions.Length
            };

            var enabledExtensionNames = SilkMarshal.StringArrayToPtr(deviceExtensions);
            createInfo.PpEnabledExtensionNames = (byte**)enabledExtensionNames;

#if VULKAN_VALIDATION
            createInfo.EnabledLayerCount = (uint)vulkanInstance.ValidationLayers.Count;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(vulkanInstance.ValidationLayers);
#else
            createInfo.EnabledLayerCount = 0;
#endif

            fixed (Device* pDevice = &device)
                if (vk.CreateDevice(vulkanPhysicalDevice.PhysicalDevice, &createInfo, null, pDevice) != Result.Success)
                    throw new NotSupportedException("Failed to create logical device.");

            fixed (Queue* pGraphicsQueue = &graphicsQueue)
                vk.GetDeviceQueue(device, graphicsFamily.Value, 0, pGraphicsQueue);

            fixed (Queue* pPresentQueue = &presentQueue)
                vk.GetDeviceQueue(device, presentFamily!.Value, 0, pPresentQueue);
        }

        vk.CurrentDevice = device;
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyDevice(this.Device, null);
        }
    }
}