using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanPhysicalDevice : VulkanDependancy
{
    public record struct QueueFamilyIndices(uint? GraphicsFamily, uint? PresentFamily)
    {
        public bool IsComplete =>
            this.GraphicsFamily.HasValue && this.PresentFamily.HasValue;
    }
    public record SwapChainSupportDetails(SurfaceCapabilitiesKHR Capabilities, SurfaceFormatKHR[] Formats,
        PresentModeKHR[] PresentModes);

    private readonly KhrSurface vkSurface;
    private readonly SurfaceKHR surface;
    private readonly PhysicalDevice physicalDevice;
    public PhysicalDevice PhysicalDevice => this.physicalDevice;

    public VulkanPhysicalDevice(Vk vk, KhrSurface vkSurface, in SurfaceKHR surface, VulkanInstance instance, string[] deviceExtensions) : base(vk)
    {
        this.vkSurface = vkSurface;
        this.surface = surface;

        var devices = vk.GetPhysicalDevices(instance.Instance);
        if (!devices.Any())
            throw new NotSupportedException("Failed to find GPUs with Vulkan support.");


        // note: should require geometry shader
        // should rate devices here
        //
        //// Discrete GPUs have a significant performance advantage
        //if (deviceProperties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
        //{
        //    score += 1000;
        //}
        //VkPhysicalDeviceFeatures supportedFeatures;
        //vkGetPhysicalDeviceFeatures(device, &supportedFeatures);

        //return indices.isComplete() && extensionsSupported && swapChainAdequate && supportedFeatures.samplerAnisotropy;
        //// Maximum possible size of textures affects graphics quality
        //score += deviceProperties.limits.maxImageDimension2D;
        foreach (var device in devices)
        {
            var indices = FindQueueFamilies(in device);

            var extensionsSupported = deviceExtensions.All(ext => vk.IsDeviceExtensionPresent(device, ext));

            if (!extensionsSupported)
                continue;

            var (_, surfaceFormatKhrs, presentModeKhrs) = QuerySwapChainSupport(in device);
            var swapChainAdequate = surfaceFormatKhrs.Length != 0 && presentModeKhrs.Length != 0;

            if (!indices.IsComplete || !extensionsSupported || !swapChainAdequate) 
                continue;
            physicalDevice = device;
            break;
        }

        if (this.physicalDevice.Handle == 0)
            throw new NotSupportedException("No suitable device.");
    }

    public QueueFamilyIndices FindQueueFamilies() =>
        FindQueueFamilies(in this.physicalDevice);

    private QueueFamilyIndices FindQueueFamilies(in PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();

        uint queryFamilyCount = 0;
        unsafe
        {
            this.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, null);

            using var mem = GlobalMemory.Allocate((int)queryFamilyCount * sizeof(QueueFamilyProperties));
            var queueFamilies = (QueueFamilyProperties*)Unsafe.AsPointer(ref mem.GetPinnableReference());

            this.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, queueFamilies);
            for (var i = 0u; i < queryFamilyCount; i++)
            {
                var queueFamily = queueFamilies[i];
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
                    indices.GraphicsFamily = i;

                this.vkSurface.GetPhysicalDeviceSurfaceSupport(device, i, this.surface, out var presentSupport);

                if (presentSupport == Vk.True)
                    indices.PresentFamily = i;

                if (indices.IsComplete)
                    break;
            }
        }
        return indices;
    }

    public SwapChainSupportDetails QuerySwapChainSupport() => this.QuerySwapChainSupport(in this.physicalDevice);
    private SwapChainSupportDetails QuerySwapChainSupport(in PhysicalDevice device)
    {
        this.vkSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out var surfaceCapabilities);

        var formatCount = 0u;
        unsafe
        {
            this.vkSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, null);

            SurfaceFormatKHR[] formats;

            if (formatCount != 0)
            {
                formats = new SurfaceFormatKHR[formatCount];

                using var mem = GlobalMemory.Allocate((int)formatCount * sizeof(SurfaceFormatKHR));
                var pFormats = (SurfaceFormatKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());

                this.vkSurface.GetPhysicalDeviceSurfaceFormats(device, this.surface, &formatCount, pFormats);

                for (var i = 0; i < formatCount; i++)
                    formats[i] = pFormats[i];
            }
            else
                formats = Array.Empty<SurfaceFormatKHR>();

            var presentModeCount = 0u;
            this.vkSurface.GetPhysicalDeviceSurfacePresentModes(device, this.surface, &presentModeCount, null);

            PresentModeKHR[] presentModes;
            if (presentModeCount != 0)
            {
                presentModes = new PresentModeKHR[presentModeCount];

                using var mem = GlobalMemory.Allocate((int)presentModeCount * sizeof(PresentModeKHR));
                var modes = (PresentModeKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());

                this.vkSurface.GetPhysicalDeviceSurfacePresentModes(device, this.surface, &presentModeCount, modes);

                for (var i = 0; i < presentModeCount; i++)
                    presentModes[i] = modes[i];
            }
            else
                presentModes = Array.Empty<PresentModeKHR>();

            return new(surfaceCapabilities, formats, presentModes);
        }
    }
    public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        this.Vk.GetPhysicalDeviceMemoryProperties(this.physicalDevice, out var memoryProperties);
        for (var i = 0; i < memoryProperties.MemoryTypeCount; i++)
            if ((typeFilter & (1u << i)) != 0 && (memoryProperties.MemoryTypes[i].PropertyFlags & properties) != 0)
                return (uint)i;
        throw new VulkanException("failed to find suitable memory type!");
    }
}