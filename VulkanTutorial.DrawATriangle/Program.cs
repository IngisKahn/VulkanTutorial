using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Image = Silk.NET.Vulkan.Image;
using ImageLayout = Silk.NET.Vulkan.ImageLayout;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.DrawATriangle
{
    internal static class Program
    {
        private const bool EnableValidationLayers = true;
        public const int MaxFramesInFlight = 8;
        private const bool EventBasedRendering = false;

        private static IWindow window;

        private static int width = 800;
        private static int height = 600;

        private static Vk vk;
        private static KhrSurface vkSurface;
        private static KhrSwapchain vkSwapchain;
        private static ExtDebugUtils debugUtils;

        private static Instance instance;
        private static DebugUtilsMessengerEXT debugMessenger;
        private static PhysicalDevice physicalDevice;
        private static Device device;
        private static SurfaceKHR surface;
        private static PipelineLayout pipelineLayout;
        private static Pipeline graphicsPipeline;
        private static RenderPass renderPass;

        private static CommandPool commandPool;
        private static CommandBuffer[] commandBuffers;

        private static Semaphore[] imageAvailableSemaphores;
        private static Semaphore[] renderFinishedSemaphores;
        private static Fence[] inFlightFences;
        private static Fence[] imagesInFlight;
        private static uint currentFrame;

        private static bool framebufferResized = false;


        private static Queue graphicsQueue;
        private static Queue presentQueue;

        private static SwapchainKHR swapchain;
        private static Image[] swapchainImages;
        private static Format swapchainImageFormat;
        private static Extent2D swapchainExtent;
        private static ImageView[] swapchainImageViews;
        private static Framebuffer[] swapchainFramebuffers;

        private static string[][] validationLayerNamesPriorityList =
        {
            new [] { "VK_LAYER_KHRONOS_validation" },
            new [] { "VK_LAYER_LUNARG_standard_validation" },
            new []
            {
                "VK_LAYER_GOOGLE_threading",
                "VK_LAYER_LUNARG_parameter_validation",
                "VK_LAYER_LUNARG_object_tracker",
                "VK_LAYER_LUNARG_core_validation",
                "VK_LAYER_GOOGLE_unique_objects",
            }
        };
        private static string[] validationLayers;
        private static string[] instanceExtensions = { ExtDebugUtils.ExtensionName };
        private static string[] deviceExtensions = { KhrSwapchain.ExtensionName };

        private static byte[] LoadEmbeddedResourceBytes(string path)
        {
            using var s = typeof(Program).Assembly.GetManifestResourceStream(path);
            using var ms = new MemoryStream((int)s.Length);
            s.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static void Main()
        {
            InitWindow();
            InitVulkan();
            MainLoop();
            CleanUp();
        }

        private static void InitVulkan()
        {
            CreateInstance();
            SetupDebugMessenger();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFrameBuffers();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSyncObjects();
        }

        private static void CreateSyncObjects()
        {
            imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
            renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
            inFlightFences = new Fence[MaxFramesInFlight];
            imagesInFlight = new Fence[MaxFramesInFlight];

            SemaphoreCreateInfo semaphoreInfo = new()
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            FenceCreateInfo fenceInfo = new()
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.FenceCreateSignaledBit
            };

            for (var i = 0; i < MaxFramesInFlight; i++)
            {
                Semaphore imgAvSema, renderFinSema;
                Fence inFlightFence;
                unsafe
                {
                    if (vk.CreateSemaphore(device, &semaphoreInfo, null, &imgAvSema) != Result.Success ||
                        vk.CreateSemaphore(device, &semaphoreInfo, null, &renderFinSema) != Result.Success ||
                        vk.CreateFence(device, &fenceInfo, null, &inFlightFence) != Result.Success)
                        throw new("failed to create synchronization objects for a frame!");
                }

                imageAvailableSemaphores[i] = imgAvSema;
                renderFinishedSemaphores[i] = renderFinSema;
                inFlightFences[i] = inFlightFence;
            }
        }

        private static void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[swapchainFramebuffers.Length];

            CommandBufferAllocateInfo allocInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length
            };

            unsafe
            {
                fixed (CommandBuffer* pCommandBuffers = commandBuffers)
                    if (vk.AllocateCommandBuffers(device, &allocInfo, pCommandBuffers) != Result.Success)
                        throw new("failed to allocate command buffers!");

                for (var i = 0; i < commandBuffers.Length; i++)
                {
                    CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

                    if (vk.BeginCommandBuffer(commandBuffers[i], &beginInfo) != Result.Success)
                        throw new("failed to begin recording command buffer!");

                    RenderPassBeginInfo renderPassInfo = new()
                    {
                        SType = StructureType.RenderPassBeginInfo,
                        RenderPass = renderPass,
                        Framebuffer = swapchainFramebuffers[i],
                        RenderArea = { Offset = new() { X = 0, Y = 0 }, Extent = swapchainExtent }
                    };

                    ClearValue clearColor = new()
                    { Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 } };
                    renderPassInfo.ClearValueCount = 1;
                    renderPassInfo.PClearValues = &clearColor;

                    vk.CmdBeginRenderPass(commandBuffers[i], &renderPassInfo, SubpassContents.Inline);

                    vk.CmdBindPipeline(commandBuffers[i], PipelineBindPoint.Graphics, graphicsPipeline);

                    vk.CmdDraw(commandBuffers[i], 3, 1, 0, 0);

                    vk.CmdEndRenderPass(commandBuffers[i]);

                    if (vk.EndCommandBuffer(commandBuffers[i]) != Result.Success)
                        throw new("failed to record command buffer!");
                }
            }
        }

        private static void CreateCommandPool()
        {
            var queueFamilyIndices = FindQueueFamilies(physicalDevice);

            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value
            };

            unsafe
            {
                fixed (CommandPool* pCommandPool = &commandPool)
                    if (vk.CreateCommandPool(device, &poolInfo, null, pCommandPool) != Result.Success)
                        throw new Exception("failed to create command pool!");
            }
        }

        private static void CreateFrameBuffers()
        {
            swapchainFramebuffers = new Framebuffer[swapchainImageViews.Length];

            for (var i = 0; i < swapchainImageViews.Length; i++)
            {
                var attachment = swapchainImageViews[i];
                Framebuffer framebuffer = new();
                unsafe
                {
                    var framebufferInfo = new FramebufferCreateInfo
                    {
                        SType = StructureType.FramebufferCreateInfo,
                        RenderPass = renderPass,
                        AttachmentCount = 1,
                        PAttachments = &attachment,
                        Width = swapchainExtent.Width,
                        Height = swapchainExtent.Height,
                        Layers = 1
                    };

                    if (vk.CreateFramebuffer(device, &framebufferInfo, null, &framebuffer) != Result.Success)
                        throw new("failed to create framebuffer!");
                }

                swapchainFramebuffers[i] = framebuffer;
            }
        }

        private static void CreateRenderPass()
        {
            AttachmentDescription colorAttachment = new()
            {
                Format = swapchainImageFormat,
                Samples = SampleCountFlags.SampleCount1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            AttachmentReference colorAttachmentRef = new()
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            unsafe
            {
                SubpassDescription subpass = new()
                {
                    PipelineBindPoint = PipelineBindPoint.Graphics,
                    ColorAttachmentCount = 1,
                    PColorAttachments = &colorAttachmentRef
                };

                SubpassDependency dependency = new()
                {
                    SrcSubpass = Vk.SubpassExternal,
                    DstSubpass = 0,
                    SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                    SrcAccessMask = 0,
                    DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                    DstAccessMask = AccessFlags.AccessColorAttachmentReadBit | AccessFlags.AccessColorAttachmentWriteBit
                };

                RenderPassCreateInfo renderPassInfo = new()
                {
                    SType = StructureType.RenderPassCreateInfo,
                    AttachmentCount = 1,
                    PAttachments = &colorAttachment,
                    SubpassCount = 1,
                    PSubpasses = &subpass,
                    DependencyCount = 1,
                    PDependencies = &dependency
                };

                fixed (RenderPass* pRenderPass = &renderPass)
                    if (vk.CreateRenderPass(device, &renderPassInfo, null, pRenderPass) != Result.Success)
                        throw new("failed to create render pass!");
            }
        }

        private static void CreateGraphicsPipeline()
        {
            var vertShaderCode = LoadEmbeddedResourceBytes("VulkanTutorial.DrawATriangle.shader.vert.spv");
            var fragShaderCode = LoadEmbeddedResourceBytes("VulkanTutorial.DrawATriangle.shader.frag.spv");

            var vertShaderModule = CreateShaderModule(vertShaderCode);
            var fragShaderModule = CreateShaderModule(fragShaderCode);

            unsafe
            {
                PipelineShaderStageCreateInfo vertShaderStageInfo = new()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageVertexBit,
                    Module = vertShaderModule,
                    PName = (byte*)SilkMarshal.StringToPtr("main")
                };

                PipelineShaderStageCreateInfo fragShaderStageInfo = new()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageFragmentBit,
                    Module = fragShaderModule,
                    PName = (byte*)SilkMarshal.StringToPtr("main")
                };

                var shaderStages = stackalloc PipelineShaderStageCreateInfo[2];
                shaderStages[0] = vertShaderStageInfo;
                shaderStages[1] = fragShaderStageInfo;

                PipelineVertexInputStateCreateInfo vertexInputInfo = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 0,
                    VertexAttributeDescriptionCount = 0
                };

                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = Vk.False
                };

                Viewport viewport = new()
                {
                    X = 0,
                    Y = 0,
                    Width = swapchainExtent.Width,
                    Height = swapchainExtent.Height,
                    MinDepth = 0,
                    MaxDepth = 1
                };

                Rect2D scissor = new() { Offset = default, Extent = swapchainExtent };

                // more than one requires setting a GPU state
                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    PViewports = &viewport,
                    ScissorCount = 1,
                    PScissors = &scissor
                };

                PipelineRasterizationStateCreateInfo rasterizer = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = Vk.False,
                    RasterizerDiscardEnable = Vk.False,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1, // bigger requires wideLine feature
                    CullMode = CullModeFlags.CullModeBackBit,
                    FrontFace = FrontFace.Clockwise,
                    DepthBiasEnable = Vk.False
                };

                PipelineMultisampleStateCreateInfo multisampling = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = Vk.False,
                    RasterizationSamples = SampleCountFlags.SampleCount1Bit
                };

                PipelineColorBlendAttachmentState colorBlendAttachment = new()
                {
                    ColorWriteMask = ColorComponentFlags.ColorComponentRBit |
                                     ColorComponentFlags.ColorComponentGBit |
                                     ColorComponentFlags.ColorComponentBBit |
                                     ColorComponentFlags.ColorComponentABit,
                    BlendEnable = Vk.False
                };

                PipelineColorBlendStateCreateInfo colorBlending = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = Vk.False,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment
                };

                colorBlending.BlendConstants[0] = 0;
                colorBlending.BlendConstants[1] = 0;
                colorBlending.BlendConstants[2] = 0;
                colorBlending.BlendConstants[3] = 0; var pipelineLayoutInfo = new PipelineLayoutCreateInfo
                {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    SetLayoutCount = 0,
                    PushConstantRangeCount = 0
                };

                fixed (PipelineLayout* pPipelineLayout = &pipelineLayout)
                    if (vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, pPipelineLayout) != Result.Success)
                        throw new("failed to create pipeline layout!");

                var pipelineInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = shaderStages,
                    PVertexInputState = &vertexInputInfo,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PColorBlendState = &colorBlending,
                    Layout = pipelineLayout,
                    RenderPass = renderPass,
                    Subpass = 0,
                    BasePipelineHandle = default
                };

                fixed (Pipeline* pGraphicsPipeline = &graphicsPipeline)
                    if (vk.CreateGraphicsPipelines
                            (device, default, 1, &pipelineInfo, null, pGraphicsPipeline) != Result.Success)
                        throw new("failed to create graphics pipeline!");

                vk.DestroyShaderModule(device, vertShaderModule, null);
                vk.DestroyShaderModule(device, fragShaderModule, null);
            }
        }

        private static ShaderModule CreateShaderModule(byte[] code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length
            };
            unsafe
            {
                fixed (byte* codePtr = code)
                    createInfo.PCode = (uint*)codePtr;

                ShaderModule shaderModule = new();
                return vk.CreateShaderModule(device, &createInfo, null, &shaderModule) != Result.Success
                    ? throw new("failed to create shader module!")
                    : shaderModule;
            }
        }

        private static void CreateImageViews()
        {
            swapchainImageViews = new ImageView[swapchainImages.Length];

            for (var i = 0; i < swapchainImages.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = swapchainImages[i],
                    ViewType = ImageViewType.ImageViewType2D,
                    Format = swapchainImageFormat,
                    Components =
                    {
                        R = ComponentSwizzle.Identity,
                        G = ComponentSwizzle.Identity,
                        B = ComponentSwizzle.Identity,
                        A = ComponentSwizzle.Identity
                    },
                    SubresourceRange =
                    {
                        AspectMask = ImageAspectFlags.ImageAspectColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };

                ImageView imageView = default;
                unsafe
                {
                    if (vk.CreateImageView(device, &createInfo, null, &imageView) != Result.Success)

                        throw new("failed to create image views!");
                }

                swapchainImageViews[i] = imageView;
            }
        }

        private static bool CreateSwapChain()
        {
            var swapChainSupport = QuerySwapChainSupport(physicalDevice);

            var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
            var presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
            var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

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

            var indices = FindQueueFamilies(physicalDevice);
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

                    if (!vk.TryGetDeviceExtension(instance, vk.CurrentDevice.Value, out vkSwapchain))
                        throw new NotSupportedException("KHR_swapchain extension not found.");

                    fixed (SwapchainKHR* pSwapchain = &swapchain)
                        if (vkSwapchain.CreateSwapchain(device, &createInfo, null, pSwapchain) != Result.Success)
                            throw new InvalidOperationException("failed to create swap chain!");
                }

                vkSwapchain.GetSwapchainImages(device, swapchain, &imageCount, null);
                swapchainImages = new Image[imageCount];
                fixed (Image* pSwapchainImage = swapchainImages)
                {
                    vkSwapchain.GetSwapchainImages(device, swapchain, &imageCount, pSwapchainImage);
                }
            }

            swapchainImageFormat = surfaceFormat.Format;
            swapchainExtent = extent;

            return true;
        }

        private static void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(physicalDevice);
            var uniqueQueueFamilies = indices.GraphicsFamily == indices.PresentFamily
                ? new[] { indices.GraphicsFamily!.Value }
                : new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

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

                if (EnableValidationLayers)
                {
                    createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                    createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
                }
                else
                    createInfo.EnabledLayerCount = 0;

                fixed (Device* pDevice = &device)
                    if (vk.CreateDevice(physicalDevice, &createInfo, null, pDevice) != Result.Success)
                        throw new NotSupportedException("Failed to create logical device.");

                fixed (Queue* pGraphicsQueue = &graphicsQueue)
                    vk.GetDeviceQueue(device, indices.GraphicsFamily.Value, 0, pGraphicsQueue);

                fixed (Queue* pPresentQueue = &presentQueue)
                    vk.GetDeviceQueue(device, indices.PresentFamily!.Value, 0, pPresentQueue);
            }

            vk.CurrentDevice = device;

            if (!vk.TryGetDeviceExtension(instance, device, out vkSwapchain))
            {
                throw new NotSupportedException("KHR_swapchain extension not found.");
            }

            Console.WriteLine($"{vk.CurrentInstance?.Handle} {vk.CurrentDevice?.Handle}");
        }

        private static void CreateSurface()
        {
            unsafe
            {
                surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
            }
        }

        private static void PickPhysicalDevice()
        {
            var devices = vk.GetPhysicalDevices(instance);
            if (!devices.Any())
                throw new NotSupportedException("Failed to find GPUs with Vulkan support.");


            // note: should require geometry shader
            // should rate devices here
            //// Discrete GPUs have a significant performance advantage
            //if (deviceProperties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU)
            //{
            //    score += 1000;
            //}

            //// Maximum possible size of textures affects graphics quality
            //score += deviceProperties.limits.maxImageDimension2D;
            physicalDevice = devices.FirstOrDefault(device =>
            {
                var indices = FindQueueFamilies(device);

                var extensionsSupported = CheckDeviceExtensionSupport(device);

                if (!extensionsSupported)
                    return false;

                var (_, surfaceFormatKhrs, presentModeKhrs) = QuerySwapChainSupport(device);
                var swapChainAdequate = surfaceFormatKhrs.Length != 0 && presentModeKhrs.Length != 0;

                return indices.IsComplete && extensionsSupported && swapChainAdequate;
            });

            if (physicalDevice.Handle == 0)
                throw new NotSupportedException("No suitable device.");
        }

        public record struct QueueFamilyIndices(uint? GraphicsFamily, uint? PresentFamily)
        {
            public bool IsComplete =>
                GraphicsFamily.HasValue && PresentFamily.HasValue;
        }

        private static bool CheckDeviceExtensionSupport(PhysicalDevice device) =>
            deviceExtensions.All(ext => vk.IsDeviceExtensionPresent(device, ext));

        public record SwapChainSupportDetails(SurfaceCapabilitiesKHR Capabilities, SurfaceFormatKHR[] Formats,
            PresentModeKHR[] PresentModes);

        private static QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            var indices = new QueueFamilyIndices();

            uint queryFamilyCount = 0;
            unsafe
            {
                vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, null);

                using var mem = GlobalMemory.Allocate((int)queryFamilyCount * sizeof(QueueFamilyProperties));
                var queueFamilies = (QueueFamilyProperties*)Unsafe.AsPointer(ref mem.GetPinnableReference());

                vk.GetPhysicalDeviceQueueFamilyProperties(device, &queryFamilyCount, queueFamilies);
                for (var i = 0u; i < queryFamilyCount; i++)
                {
                    var queueFamily = queueFamilies[i];
                    if (queueFamily.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
                        indices.GraphicsFamily = i;

                    vkSurface.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

                    if (presentSupport == Vk.True)
                        indices.PresentFamily = i;

                    if (indices.IsComplete)
                        break;
                }
            }
            return indices;
        }

        private static SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            vkSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out var surfaceCapabilities);

            var formatCount = 0u;
            unsafe
            {
                vkSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, null);

                SurfaceFormatKHR[] formats;

                if (formatCount != 0)
                {
                    formats = new SurfaceFormatKHR[formatCount];

                    using var mem = GlobalMemory.Allocate((int)formatCount * sizeof(SurfaceFormatKHR));
                    var pFormats = (SurfaceFormatKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());

                    vkSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, pFormats);

                    for (var i = 0; i < formatCount; i++)
                        formats[i] = pFormats[i];
                }
                else
                    formats = Array.Empty<SurfaceFormatKHR>();

                var presentModeCount = 0u;
                vkSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, &presentModeCount, null);

                PresentModeKHR[] presentModes;
                if (presentModeCount != 0)
                {
                    presentModes = new PresentModeKHR[presentModeCount];

                    using var mem = GlobalMemory.Allocate((int)presentModeCount * sizeof(PresentModeKHR));
                    var modes = (PresentModeKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());

                    vkSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, &presentModeCount, modes);

                    for (var i = 0; i < presentModeCount; i++)
                        presentModes[i] = modes[i];
                }
                else
                    presentModes = Array.Empty<PresentModeKHR>();

                return new(surfaceCapabilities, formats, presentModes);
            }
        }

        private static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
                return capabilities.CurrentExtent;

            var actualExtent = new Extent2D
            { Height = (uint)window.FramebufferSize.Y, Width = (uint)window.FramebufferSize.X };
            actualExtent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, actualExtent.Width));
            actualExtent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, actualExtent.Height));

            return actualExtent;
        }

        private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] presentModes) =>
            presentModes.FirstOrDefault(p => p == PresentModeKHR.PresentModeMailboxKhr, PresentModeKHR.PresentModeFifoKhr);

        private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] formats) =>
            formats.FirstOrDefault(f => f.Format == Format.B8G8R8A8Unorm, formats[0]);

        private static void SetupDebugMessenger()
        {
            if (!EnableValidationLayers) return;
            if (!vk.TryGetInstanceExtension(instance, out debugUtils)) return;

            var createInfo = new DebugUtilsMessengerCreateInfoEXT();
            PopulateDebugMessengerCreateInfo(ref createInfo);
            unsafe
            {
                fixed (DebugUtilsMessengerEXT* pDebugMessenger = &debugMessenger)
                    if (debugUtils.CreateDebugUtilsMessenger
                            (instance, &createInfo, null, pDebugMessenger) != Result.Success)
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
        }

        private static void CreateInstance()
        {
            vk = Vk.GetApi();

            if (EnableValidationLayers)
                validationLayers = GetOptimalValidationLayers() ??
                                   throw new NotSupportedException("Validation layers requested, but not available!");

            unsafe
            {
                uint extensionCount;
                vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, null);
                var extensionProperties = stackalloc ExtensionProperties[(int)extensionCount];
                vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, extensionProperties);
                Console.WriteLine("Available extensions:");
                for (var i = 0; i < extensionCount; i++)
                    Console.WriteLine("\t" + Marshal.PtrToStringAnsi((IntPtr)extensionProperties[i].ExtensionName));

                ApplicationInfo appInfo = new()
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
                    ApplicationVersion = new Version32(1, 0, 0),
                    PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                    EngineVersion = new Version32(1, 0, 0),
                    ApiVersion = Vk.Version11
                };

                InstanceCreateInfo createInfo = new()
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo
                };
                var extensions = window.VkSurface!.GetRequiredExtensions(out var extCount);
                var newExtensions = stackalloc byte*[(int)(extCount)];// + _instanceExtensions.Length)];
                for (var i = 0; i < extCount; i++)
                    newExtensions[i] = extensions[i];

                for (var i = 0; i < instanceExtensions.Length; i++)

                    newExtensions[extCount + i] = (byte*)SilkMarshal.StringToPtr(instanceExtensions[i]);


                extCount += (uint)instanceExtensions.Length;
                createInfo.EnabledExtensionCount = extCount;
                createInfo.PpEnabledExtensionNames = newExtensions;

                DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
                if (EnableValidationLayers)
                {
                    createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                    createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
                    PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
                    createInfo.PNext = &debugCreateInfo;
                }

                fixed (Instance* pInstance = &instance)
                    if (vk.CreateInstance(&createInfo, null, pInstance) != Result.Success)
                        throw new InvalidOperationException("Failed to create instance!");

                vk.CurrentInstance = instance;

                if (!vk.TryGetInstanceExtension(instance, out vkSurface))
                    throw new NotSupportedException("KHR_surface extension not found.");

                Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
                Marshal.FreeHGlobal((nint)appInfo.PEngineName);

                if (EnableValidationLayers)
                    SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
            }
        }

        private static string[]? GetOptimalValidationLayers()
        {
            string?[] availableLayerNames;
            unsafe
            {
                var layerCount = 0u;
                vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)0);

                var availableLayers = new LayerProperties[layerCount];
                fixed (LayerProperties* availableLayersPtr = availableLayers)
                    vk.EnumerateInstanceLayerProperties(&layerCount, availableLayersPtr);

                availableLayerNames = availableLayers
                    .Select(availableLayer => Marshal.PtrToStringAnsi((nint)availableLayer.LayerName)).ToArray();
            }

            return validationLayerNamesPriorityList
                .FirstOrDefault(validationLayerNameSet => validationLayerNameSet.All(validationLayerName => availableLayerNames.Contains(validationLayerName)));
        }

        private static void MainLoop()
        {
            window.Render += DrawFrame;
            window.Run();
            vk.DeviceWaitIdle(device);
        }

        private static void DrawFrame(double obj)
        {
            var fence = inFlightFences[currentFrame];
            vk.WaitForFences(device, 1, in fence, Vk.True, ulong.MaxValue);

            uint imageIndex;
            unsafe
            {
                Result result = vkSwapchain.AcquireNextImage
                    (device, swapchain, ulong.MaxValue, imageAvailableSemaphores[currentFrame], default, &imageIndex);

                if (result == Result.ErrorOutOfDateKhr)
                {
                    RecreateSwapChain();
                    return;
                }
                if (result is not Result.Success and not Result.SuboptimalKhr)
                    throw new("failed to acquire swap chain image!");

                if (imagesInFlight[imageIndex].Handle != 0)
                    vk.WaitForFences(device, 1, in imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);

                imagesInFlight[imageIndex] = inFlightFences[currentFrame];

                SubmitInfo submitInfo = new() { SType = StructureType.SubmitInfo };

                Semaphore[] waitSemaphores = { imageAvailableSemaphores[currentFrame] };
                PipelineStageFlags[] waitStages = { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };
                submitInfo.WaitSemaphoreCount = 1;
                var signalSemaphore = renderFinishedSemaphores[currentFrame];
                fixed (Semaphore* waitSemaphoresPtr = waitSemaphores)
                {
                    fixed (PipelineStageFlags* waitStagesPtr = waitStages)
                    {
                        submitInfo.PWaitSemaphores = waitSemaphoresPtr;
                        submitInfo.PWaitDstStageMask = waitStagesPtr;

                        submitInfo.CommandBufferCount = 1;
                        var buffer = commandBuffers[imageIndex];
                        submitInfo.PCommandBuffers = &buffer;

                        submitInfo.SignalSemaphoreCount = 1;
                        submitInfo.PSignalSemaphores = &signalSemaphore;

                        vk.ResetFences(device, 1, &fence);

                        if (vk.QueueSubmit
                                (graphicsQueue, 1, &submitInfo, inFlightFences[currentFrame]) != Result.Success)
                            throw new("failed to submit draw command buffer!");
                    }
                }

                fixed (SwapchainKHR* pSwapchain = &swapchain)
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

                    result = vkSwapchain.QueuePresent(presentQueue, &presentInfo);
                }

                if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr || framebufferResized)
                {
                    framebufferResized = false;
                    RecreateSwapChain();
                }
                else if (result != Result.Success)
                    throw new("failed to present swap chain image!");
            }

            currentFrame = (currentFrame + 1) % MaxFramesInFlight;
        }

        private static void RecreateSwapChain()
        {
            var framebufferSize = window.FramebufferSize;

            while (framebufferSize.X == 0 || framebufferSize.Y == 0)
            {
                framebufferSize = window.FramebufferSize;
                window.DoEvents();
            }

            vk.DeviceWaitIdle(device);

            CleanUpSwapchain();

            // TODO: On SDL it is possible to get an invalid swap chain when the window is minimized.
            // This check can be removed when the above frameBufferSize check catches it.
            while (!CreateSwapChain()) 
                window.DoEvents();

            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFrameBuffers();
            CreateCommandBuffers();

            imagesInFlight = new Fence[swapchainImages.Length];
        }

        private static void CleanUp()
        {
            CleanUpSwapchain();
            unsafe
            {

                for (var i = 0; i < MaxFramesInFlight; i++)
                {
                    vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
                    vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
                    vk.DestroyFence(device, inFlightFences[i], null);
                }

                vk.DestroyCommandPool(device, commandPool, null);

                vk.DestroyDevice(device, null);

                if (EnableValidationLayers)
                    debugUtils.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
                vkSurface.DestroySurface(instance, surface, null);
                vk.DestroyInstance(instance, null);
            }
        }

        private static void CleanUpSwapchain()
        {
            unsafe
            {
                foreach (var framebuffer in swapchainFramebuffers)
                    vk.DestroyFramebuffer(device, framebuffer, null);

                vk.DestroyPipeline(device, graphicsPipeline, null);
                vk.DestroyPipelineLayout(device, pipelineLayout, null);
                vk.DestroyRenderPass(device, renderPass, null);

                foreach (var imageView in swapchainImageViews)
                    vk.DestroyImageView(device, imageView, null);

                vkSwapchain.DestroySwapchain(device, swapchain, null);
            }
        }

        private static void InitWindow()
        {
            var options = WindowOptions.DefaultVulkan;
            options.Size = new(width, height);
            options.Title = "Vulkan with Silk.NET";
            options.IsEventDriven = EventBasedRendering;
            window = Window.Create(options);
            window.Initialize(); // For safety the window should be initialized before querying the VkSurface

            if (window.VkSurface is null)
                throw new NotSupportedException("Windowing platform doesn't support Vulkan.");

            window.FramebufferResize += OnFramebufferResize;
        }

        private static void OnFramebufferResize(Vector2D<int> obj)
        {
            framebufferResized = true;
            RecreateSwapChain();
            window.DoRender();
        }
    }
}