using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.UniformBuffers;

public class VulkanSwapChain : IDisposable
{
    private readonly KhrSwapchain vkSwapchain;
    private readonly SwapchainKHR swapchain;
    public SwapchainKHR Swapchain => this.swapchain;
    private readonly Image[] swapchainImages;
    public Image[] SwapchainImages => this.swapchainImages;
    private readonly Format swapchainImageFormat;
    public Format SwapchainImageFormat => this.swapchainImageFormat;
    private readonly Extent2D swapchainExtent;
    public Extent2D SwapchainExtent => this.swapchainExtent;

    private readonly VulkanVirtualDevice device;

    private readonly VulkanImageViews imageViews;
    public VulkanImageViews ImageViews => this.imageViews;
    public VulkanRenderPass RenderPass { get; }
    public VulkanGraphicsPipeline GraphicsPipeline { get; }
    public VulkanFrameBuffers FrameBuffers { get; }

    public VulkanSwapChain(Vk vk, IWindow window, VulkanInstance instance, VulkanPhysicalDevice physicalDevice,
        VulkanVirtualDevice device, in SurfaceKHR surface, VulkanDescriptorSetLayout descriptorSetLayout)
    {
        this.device = device;
        var swapChainSupport = physicalDevice.QuerySwapChainSupport();

        var surfaceFormat =
            swapChainSupport.Formats.FirstOrDefault(f => f.Format == Format.B8G8R8Unorm, swapChainSupport.Formats[0]);
        var presentMode = swapChainSupport.PresentModes.FirstOrDefault(p => p == PresentModeKHR.PresentModeMailboxKhr,
            PresentModeKHR.PresentModeFifoKhr);
        Extent2D extent;

        if (swapChainSupport.Capabilities.CurrentExtent.Width != uint.MaxValue)
            extent = swapChainSupport.Capabilities.CurrentExtent;
        else
        {
            extent = new()
                { Height = (uint)window.FramebufferSize.Y, Width = (uint)window.FramebufferSize.X };
            extent.Width = Math.Max(swapChainSupport.Capabilities.MinImageExtent.Width,
                Math.Min(swapChainSupport.Capabilities.MaxImageExtent.Width, extent.Width));
            extent.Height = Math.Max(swapChainSupport.Capabilities.MinImageExtent.Height,
                Math.Min(swapChainSupport.Capabilities.MaxImageExtent.Height, extent.Height));
        }

        //// TODO: On SDL minimizing the window does not affect the frameBufferSize.
        //// This check can be removed if it does
        //if (extent.Width == 0 || extent.Height == 0)
        //    return false;

        var imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
        if (swapChainSupport.Capabilities.MaxImageCount > 0 &&
            imageCount > swapChainSupport.Capabilities.MaxImageCount)
            imageCount = swapChainSupport.Capabilities.MaxImageCount;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit
        };

        var indices = physicalDevice.FindQueueFamilies();
        uint[] queueFamilyIndices = { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

        unsafe
        {
            fixed (uint* qfiPtr = queueFamilyIndices)
            {
                if (indices.GraphicsFamily != indices.PresentFamily)
                {
                    createInfo.ImageSharingMode = SharingMode.Concurrent;
                    createInfo.QueueFamilyIndexCount = 2;
                    createInfo.PQueueFamilyIndices = qfiPtr;
                }
                else
                    createInfo.ImageSharingMode = SharingMode.Exclusive;

                createInfo.PreTransform = swapChainSupport.Capabilities.CurrentTransform;
                createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
                createInfo.PresentMode = presentMode;
                createInfo.Clipped = Vk.True;

                createInfo.OldSwapchain = default;

                if (!vk.TryGetDeviceExtension(instance.Instance, vk.CurrentDevice.Value, out vkSwapchain))
                    throw new NotSupportedException("KHR_swapchain extension not found.");

                fixed (SwapchainKHR* pSwapchain = &swapchain)
                    if (vkSwapchain.CreateSwapchain(device.Device, &createInfo, null, pSwapchain) != Result.Success)
                        throw new InvalidOperationException("failed to create swap chain!");
            }

            vkSwapchain.GetSwapchainImages(device.Device, swapchain, &imageCount, null);
            swapchainImages = new Image[imageCount];
            fixed (Image* pSwapchainImage = swapchainImages)
            {
                vkSwapchain.GetSwapchainImages(device.Device, swapchain, &imageCount, pSwapchainImage);
            }
        }

        swapchainImageFormat = surfaceFormat.Format;
        swapchainExtent = extent;

        this.imageViews = new(vk, device, this);
        this.RenderPass = new(vk, device, this);
        this.GraphicsPipeline = new(vk, device, this, descriptorSetLayout);
        this.FrameBuffers = new(vk, device, this);
    }

    public void Dispose()
    {
        unsafe
        {
            this.FrameBuffers.Dispose();
            this.GraphicsPipeline.Dispose();
            this.RenderPass.Dispose();
            this.imageViews.Dispose();
            this.vkSwapchain.DestroySwapchain(this.device.Device, this.swapchain, null);
        }
    }

    public Result QueuePresent(in Queue presentQueue, Semaphore signalSemaphore, uint imageIndex)
    {
        unsafe
        {
            fixed (SwapchainKHR* pSwapchain = &this.swapchain)
            {
                PresentInfoKHR presentInfo = new()
                {
                    SType = StructureType.PresentInfoKhr,
                    WaitSemaphoreCount = 1,
                    PWaitSemaphores = &signalSemaphore,
                    SwapchainCount = 1,
                    PSwapchains = pSwapchain,
                    PImageIndices = &imageIndex
                };
                return this.vkSwapchain.QueuePresent(presentQueue, &presentInfo);
            }
        }
    }

    public Result AcquireNextImage(Semaphore imageAvailableSemaphore, out uint imageIndex)
    {
        uint index;
        Result result;
        unsafe
        {
            result = this.vkSwapchain.AcquireNextImage
            (this.device.Device, this.swapchain, ulong.MaxValue, imageAvailableSemaphore, default,
                &index);
        }
        imageIndex = index;
        return result;
    }
}
