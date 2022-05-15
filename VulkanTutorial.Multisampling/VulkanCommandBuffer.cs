using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanCommandBuffer : VulkanDeviceDependancy, IDisposable
{
    public readonly CommandBuffer Buffer;
    private readonly CommandPool commandPool;

    public VulkanCommandBuffer(Vk vk, VulkanVirtualDevice device, CommandPool commandPool) : base(vk, device)
    {
        this.commandPool = commandPool;
        unsafe
        {
            CommandBufferAllocateInfo allocateInfo = new(level: CommandBufferLevel.Primary, commandPool: commandPool, commandBufferCount: 1);
            vk.AllocateCommandBuffers(device.Device, in allocateInfo, out this.Buffer);

            CommandBufferBeginInfo beginInfo = new(flags: CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

            vk.BeginCommandBuffer(this.Buffer, in beginInfo);
        }
    }

    public void Dispose()
    {
        this.Vk.EndCommandBuffer(this.Buffer);
        unsafe
        {
            fixed (CommandBuffer* pCommandBuffer = &this.Buffer)
            {
                SubmitInfo submitInfo = new(commandBufferCount: 1, pCommandBuffers: pCommandBuffer);

                this.Vk.QueueSubmit(this.Device.GraphicsQueue, 1, in submitInfo, new());

                this.Vk.QueueWaitIdle(this.Device.GraphicsQueue);
            }
        }

        this.Vk.FreeCommandBuffers(this.Device.Device, this.commandPool, 1, in this.Buffer);
    }
}
