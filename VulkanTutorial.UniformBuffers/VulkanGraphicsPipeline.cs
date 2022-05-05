using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System.Reflection;

namespace VulkanTutorial.UniformBuffers;

public class VulkanGraphicsPipeline : VulkanDeviceDependancy, IDisposable
{
    private readonly Pipeline pipeline;
    public Pipeline Pipeline => this.pipeline;

    private readonly PipelineLayout pipelineLayout;
    public PipelineLayout PipelineLayout => this.pipelineLayout;

    public VulkanGraphicsPipeline(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain, VulkanDescriptorSetLayout descriptorSetLayout) : base(vk, device)
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        var vertShaderCode = VulkanGraphicsPipeline.LoadEmbeddedResourceBytes(assemblyName + ".shader.vert.spv");
        var fragShaderCode = VulkanGraphicsPipeline.LoadEmbeddedResourceBytes(assemblyName + ".shader.frag.spv");

        var vertShaderModule = this.CreateShaderModule(vertShaderCode);
        var fragShaderModule = this.CreateShaderModule(fragShaderCode);

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

            var bindingDescription = Vertex.BindingDescription;
            var attributeDescriptions = Vertex.AttributeDescriptions;
            fixed (VertexInputAttributeDescription* pAttributeDescriptions = attributeDescriptions)
            {
                PipelineVertexInputStateCreateInfo vertexInputInfo = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 1,
                    VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                    PVertexBindingDescriptions = &bindingDescription,
                    PVertexAttributeDescriptions = pAttributeDescriptions
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
                    Width = swapChain.SwapchainExtent.Width,
                    Height = swapChain.SwapchainExtent.Height,
                    MinDepth = 0,
                    MaxDepth = 1
                };

                Rect2D scissor = new() { Offset = default, Extent = swapChain.SwapchainExtent };

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
                    FrontFace = FrontFace.CounterClockwise,
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
                colorBlending.BlendConstants[3] = 0;

                fixed (DescriptorSetLayout* pLayout = &descriptorSetLayout.Layout)
                {

                    var pipelineLayoutInfo = new PipelineLayoutCreateInfo
                    {
                        SType = StructureType.PipelineLayoutCreateInfo,
                        SetLayoutCount = 1,
                        PSetLayouts = pLayout,
                        PushConstantRangeCount = 0
                    };

                    fixed (PipelineLayout* pPipelineLayout = &pipelineLayout)
                        if (vk.CreatePipelineLayout(device.Device, &pipelineLayoutInfo, null, pPipelineLayout) != Result.Success)
                            throw new("failed to create pipeline layout!");
                }
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
                    RenderPass = swapChain.RenderPass.RenderPass,
                    Subpass = 0,
                    BasePipelineHandle = default
                };

                fixed (Pipeline* pGraphicsPipeline = &this.pipeline)
                    if (vk.CreateGraphicsPipelines
                            (device.Device, default, 1, &pipelineInfo, null, pGraphicsPipeline) != Result.Success)
                        throw new("failed to create graphics pipeline!");
            }

            vk.DestroyShaderModule(device.Device, vertShaderModule, null);
            vk.DestroyShaderModule(device.Device, fragShaderModule, null);
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyPipeline(this.Device.Device, this.pipeline, null);
            this.Vk.DestroyPipelineLayout(this.Device.Device, this.pipelineLayout, null);
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
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
            return this.Vk.CreateShaderModule(this.Device.Device, &createInfo, null, &shaderModule) != Result.Success
                ? throw new("failed to create shader module!")
                : shaderModule;
        }
    }
    private static byte[] LoadEmbeddedResourceBytes(string path)
    {
        using var s = typeof(Program).Assembly.GetManifestResourceStream(path);
        if (s == null)
            throw new VulkanException("Could not load embedded resource: " + path);
        using var ms = new MemoryStream((int)s.Length);
        s.CopyTo(ms);
        return ms.ToArray();
    }
}