using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanCommandBuffers
{
    private readonly CommandBuffer[] commandBuffers;
    public CommandBuffer this[int i] => this.commandBuffers[i];

    public VulkanCommandBuffers(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain, VulkanCommandPool commandPool, VulkanIndexBuffer indexBuffer, VulkanVertexBuffer vertexBuffer, int indexCount, VulkanDescriptorSetsBase descriptorSets)
    {
        this.commandBuffers = new CommandBuffer[swapChain.FrameBuffers.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)this.commandBuffers.Length
        };

        unsafe
        {
            fixed (CommandBuffer* pCommandBuffers = this.commandBuffers)
                if (vk.AllocateCommandBuffers(device.Device, &allocInfo, pCommandBuffers) != Result.Success)
                    throw new("failed to allocate command buffers!");

            for (var i = 0; i < this.commandBuffers.Length; i++)
            {
                CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };

                if (vk.BeginCommandBuffer(this.commandBuffers[i], &beginInfo) != Result.Success)
                    throw new("failed to begin recording command buffer!");

                RenderPassBeginInfo renderPassInfo = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = swapChain.RenderPass.RenderPass,
                    Framebuffer = swapChain.FrameBuffers[i],
                    RenderArea = { Offset = new() { X = 0, Y = 0 }, Extent = swapChain.SwapchainExtent }
                };

                ClearValue clearColor = new()
                { Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 } };
                renderPassInfo.ClearValueCount = 1;
                renderPassInfo.PClearValues = &clearColor;

                vk.CmdBeginRenderPass(this.commandBuffers[i], &renderPassInfo, SubpassContents.Inline);

                vk.CmdBindPipeline(this.commandBuffers[i], PipelineBindPoint.Graphics, swapChain.GraphicsPipeline.Pipeline);

                vertexBuffer.Bind(in this.commandBuffers[i]);
                indexBuffer.Bind(in this.commandBuffers[i]);

                fixed (DescriptorSet* pDescriptorSets = descriptorSets.DescriptorSets)
                    vk.CmdBindDescriptorSets(this.commandBuffers[i], PipelineBindPoint.Graphics, swapChain.GraphicsPipeline.PipelineLayout, 0, 1, &pDescriptorSets[i], 0, null);

                vk.CmdDrawIndexed(this.commandBuffers[i], (uint)indexCount, 1, 0, 0, 0);
                //vk.CmdDraw(this.commandBuffers[i], (uint), 1, 0, 0);

                vk.CmdEndRenderPass(this.commandBuffers[i]);

                if (vk.EndCommandBuffer(this.commandBuffers[i]) != Result.Success)
                    throw new("failed to record command buffer!");
            }
        }
    }
}