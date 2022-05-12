using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using Matrix4 = Silk.NET.Maths.Matrix4X4<float>;
using Silk.NET.Maths;
using System.Reflection;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanRenderer : IDisposable
{
    private readonly VulkanWindow window;
    private readonly Vk vk;
    private readonly VulkanInstance instance;
    private readonly KhrSurface surfaceExtension;
#if VULKAN_VALIDATION
    private ExtDebugUtils? debugUtils;
    private DebugUtilsMessengerEXT debugMessenger;
#endif
    private readonly SurfaceKHR surface;
    private readonly VulkanPhysicalDevice physicalDevice;
    private readonly VulkanVirtualDevice device;
    private VulkanSwapChain swapchain;
    private readonly VulkanCommandPool commandPool;
    private VulkanRenderCommandBuffers? commandBuffers;

    private VulkanSyncObjects? syncObjects;
    private uint currentFrame;

    private bool framebufferResized;

    private VulkanTextureImage? textureImage;
    private VulkanTextureSampler? textureSampler;

    private readonly VulkanUniformBuffer<UniformBufferedObject>[] uniformBuffers = new VulkanUniformBuffer<UniformBufferedObject>[VulkanSyncObjects.MaxFramesInFlight];
    private VulkanVertexBuffer? vertexBuffer;
    private VulkanIndexBuffer? indexBuffer;
    private Fence[]? imagesInFlight;

    private readonly VulkanDescriptorSetLayout descriptorSetLayout;
    private VulkanDescriptorSets<UniformBufferedObject>? descriptorSets;

    private static readonly string[] deviceExtensions = { KhrSwapchain.ExtensionName };

    private static readonly Vertex[] vertices =
    {
        new(new(-.5f, -.5f), new(1, 0, 0), new(1, 0)),
        new(new(.5f, -.5f), new(0, 1, 0), new(0, 0)),
        new(new(.5f, .5f), new(0, 0, 1), new(0, 1)),
        new(new(-.5f, .5f), new(1, 1, 1), new(1, 1))
    };

    private static readonly ushort[] indices = { 0, 1, 2, 2, 3, 0 };

    public VulkanRenderer(VulkanWindow window)
    {
        this.window = window;
        window.OnResetRenderer += this.Window_OnResetRenderer;
        this.vk = Vk.GetApi();
        this.instance = new(this.vk, window.Window);
        if (!this.vk.TryGetInstanceExtension(this.instance.Instance, out this.surfaceExtension))
            throw new NotSupportedException("KHR_surface extension not found.");

        this.SetupDebugMessenger();

        unsafe
        {
            this.surface = window.Window.VkSurface!.Create<AllocationCallbacks>(this.instance.Instance.ToHandle(), null).ToSurface();
        }

        this.physicalDevice = new(this.vk, this.surfaceExtension, in this.surface, this.instance, deviceExtensions);
        this.device = new(this.vk, this.instance, this.physicalDevice, deviceExtensions);

#if VULKAN_VALIDATION
        Console.WriteLine($"{this.vk.CurrentInstance?.Handle:X} {this.vk.CurrentDevice?.Handle:X}");
#endif

        this.descriptorSetLayout = new(this.vk, this.device);

        this.swapchain = new(this.vk, window.Window, this.instance, this.physicalDevice, this.device, in this.surface, this.descriptorSetLayout);

        this.CreateUniformBuffers();
        this.commandPool = new(this.vk, this.physicalDevice, this.device);

        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        using var s = typeof(Program).Assembly.GetManifestResourceStream(assemblyName + ".texture.jpg");
        this.textureImage = new(this.vk, this.device, this.commandPool, s);
        this.textureSampler = new(this.vk, this.device);
        this.descriptorSets = new(this.vk, this.device, this.descriptorSetLayout, this.uniformBuffers, this.textureImage, this.textureSampler);

        this.vertexBuffer = new(this.vk, this.device, this.commandPool, vertices);
        this.indexBuffer = new(this.vk, this.device, this.commandPool, indices);


        this.commandBuffers = new(this.vk, this.device, this.swapchain, this.commandPool, this.indexBuffer, this.vertexBuffer, indices.Length, descriptorSets);
        this.syncObjects = new(this.vk, this.device);
        this.imagesInFlight = new Fence[this.swapchain.ImageViews.Length];

        this.vk.DeviceWaitIdle(this.device.Device);
        this.window.Window.Render += this.DrawFrame;
    }

    private void CreateUniformBuffers()
    {
        for (var i = 0; i < this.uniformBuffers.Length; i++)
            this.uniformBuffers[i] = new(this.vk, this.device);
    }

    private void Window_OnResetRenderer(object? sender, EventArgs e)
    {
        this.framebufferResized = true;
        this.ResetSwapChain();
    }

    private void ResetSwapChain()
    {
        this.vk.DeviceWaitIdle(this.device.Device);

        //foreach (var uniformBuffer in this.uniformBuffers)
        //    uniformBuffer.Dispose();

        this.swapchain.Dispose();

        //this.CreateUniformBuffers();

        // TODO: On SDL it is possible to get an invalid swap chain when the window is minimized.
        // This check can be removed when the above frameBufferSize check catches it.
        //while (!CreateSwapChain())
        //    window.DoEvents();
        this.swapchain = new(this.vk, this.window.Window, this.instance, this.physicalDevice, this.device, in this.surface, this.descriptorSetLayout);


        this.commandBuffers = new VulkanRenderCommandBuffers(this.vk, this.device, this.swapchain, this.commandPool, this.indexBuffer, this.vertexBuffer, indices.Length, this.descriptorSets);

        this.imagesInFlight = new Fence[this.swapchain.SwapchainImages.Length];
    }

    private void WaitForWindowThenReset()
    {
        this.window.HandleBadFrameBuffer();
        this.ResetSwapChain();
    }

    [Conditional("VULKAN_VALIDATION")]
    private void SetupDebugMessenger()
    {
#if VULKAN_VALIDATION
        if (!this.vk.TryGetInstanceExtension(this.instance.Instance, out this.debugUtils))
            return;

        var createInfo = new DebugUtilsMessengerCreateInfoEXT();
        PopulateDebugMessengerCreateInfo(ref createInfo);
        unsafe
        {
            fixed (DebugUtilsMessengerEXT* pDebugMessenger = &this.debugMessenger)
                if (this.debugUtils?.CreateDebugUtilsMessenger
                        (this.instance.Instance, &createInfo, null, pDebugMessenger) != Result.Success)
                    throw new("Failed to create debug messenger.");
        }
    }

    private static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt;
        unsafe
        {
            createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
        }
    }
    private static unsafe uint DebugCallback
    (
        DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        if (messageSeverity > DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt)
            Console.WriteLine
                ($"{messageSeverity} {messageTypes}" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False;

#endif
    }

    public void Dispose()
    {
        this.window.OnResetRenderer -= this.Window_OnResetRenderer;
        this.window.Window.Render -= this.DrawFrame;

        this.descriptorSetLayout.Dispose();

        foreach (var uniformBuffer in this.uniformBuffers)
            uniformBuffer.Dispose();

        this.textureImage.Dispose();
        this.textureSampler.Dispose();

        this.descriptorSets.Dispose();

        this.swapchain.Dispose();
        this.indexBuffer.Dispose();
        this.vertexBuffer.Dispose();

        this.syncObjects.Dispose();

        this.commandPool.Dispose();

        this.device.Dispose();
        unsafe
        {

#if VULKAN_VALIDATION
            this.debugUtils?.DestroyDebugUtilsMessenger(this.instance.Instance, this.debugMessenger, null);
#endif
            this.surfaceExtension.DestroySurface(this.instance.Instance, this.surface, null);
        }
        this.instance.Dispose();
    }
    public void DrawFrame(double obj)
    {
        var (imageAvailableSemaphore, renderFinishedSemaphore, fence) = this.syncObjects[(int)this.currentFrame];
        this.vk.WaitForFences(this.device.Device, 1, in fence, Vk.True, ulong.MaxValue);

        unsafe
        {
            var result = this.swapchain.AcquireNextImage(imageAvailableSemaphore, out var imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                this.WaitForWindowThenReset();
                return;
            }
            if (result is not Result.Success and not Result.SuboptimalKhr)
                throw new VulkanException("failed to acquire swap chain image!");

            if (imagesInFlight[imageIndex].Handle != 0)
                vk.WaitForFences(device.Device, 1, in imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);

            imagesInFlight[imageIndex] = fence;

            this.UpdateUniformBuffer((int)imageIndex);

            SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo };

            Semaphore[] waitSemaphores = { imageAvailableSemaphore };
            PipelineStageFlags[] waitStages = { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };
            submitInfo.WaitSemaphoreCount = 1;
            var signalSemaphore = renderFinishedSemaphore;
            fixed (Semaphore* waitSemaphoresPtr = waitSemaphores)
            {
                fixed (PipelineStageFlags* waitStagesPtr = waitStages)
                {
                    submitInfo.PWaitSemaphores = waitSemaphoresPtr;
                    submitInfo.PWaitDstStageMask = waitStagesPtr;

                    submitInfo.CommandBufferCount = 1;
                    var buffer = commandBuffers[(int)imageIndex];
                    submitInfo.PCommandBuffers = &buffer;

                    submitInfo.SignalSemaphoreCount = 1;
                    submitInfo.PSignalSemaphores = &signalSemaphore;

                    vk.ResetFences(device.Device, 1, &fence);

                    if (vk.QueueSubmit
                            (device.GraphicsQueue, 1, &submitInfo, fence) != Result.Success)
                        throw new VulkanException("failed to submit draw command buffer!");
                }
            }

            result = swapchain.QueuePresent(device.PresentQueue, signalSemaphore, imageIndex);

            if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr || framebufferResized)
            {
                framebufferResized = false;
                this.WaitForWindowThenReset();
            }
            else if (result != Result.Success)
                throw new VulkanException("failed to present swap chain image!");
        }

        currentFrame = (currentFrame + 1) % VulkanSyncObjects.MaxFramesInFlight;
    }

    private static readonly Stopwatch startTime = new();
    static VulkanRenderer() => startTime.Start();

    private void UpdateUniformBuffer(int imageIndex)
    {
        var buffer = this.uniformBuffers[imageIndex];
        var time = startTime.ElapsedMilliseconds / 1000f;

        UniformBufferedObject ubo = new()
        {
            Model = Matrix4X4.CreateRotationZ(Scalar.DegreesToRadians(90f) * time),
            View = Matrix4X4.CreateLookAt(new Vector3D<float>(2f), Vector3D<float>.Zero, Vector3D<float>.UnitZ),
            Projection = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45f), this.swapchain.SwapchainExtent.Width / (float)this.swapchain.SwapchainExtent.Height, .1f, 10)
        };
        ubo.Projection.M22 *= -1;

        buffer.CopyData(in ubo);
    }

    internal void WaitForIdle() => this.vk.DeviceWaitIdle(this.device.Device);
}
