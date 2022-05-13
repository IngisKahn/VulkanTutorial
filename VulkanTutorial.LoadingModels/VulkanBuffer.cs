using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public class VulkanBuffer : VulkanDeviceDependancy, IDisposable
{
    protected readonly Silk.NET.Vulkan.Buffer buffer;
    public Silk.NET.Vulkan.Buffer Buffer => this.buffer;

    protected readonly DeviceMemory memory;
    public DeviceMemory Memory => this.memory;

    protected ulong Size { get; }

    public VulkanBuffer(Vk vk, VulkanVirtualDevice device, ulong size, BufferUsageFlags usageFlags, MemoryPropertyFlags propertyFlags) : base(vk, device)
    {
        this.Size = size;
        unsafe
        {
            BufferCreateInfo bufferInfo = new(size: size, usage: usageFlags, sharingMode: SharingMode.Exclusive);
            if (vk.CreateBuffer(device.Device, in bufferInfo, null, out this.buffer) != Result.Success)
                throw new VulkanException("failed to create buffer!");

            vk.GetBufferMemoryRequirements(device.Device, this.buffer, out var memoryRequirements);

            MemoryAllocateInfo allocInfo = new(allocationSize: memoryRequirements.Size, memoryTypeIndex: device.PhysicalDevice.FindMemoryType(memoryRequirements.MemoryTypeBits, propertyFlags));

            // TODO: this is bad, use VMASharp
            fixed (DeviceMemory* pVertexBufferMemory = &this.memory)
                if (vk.AllocateMemory(device.Device, in allocInfo, null, pVertexBufferMemory) != Result.Success)
                    throw new VulkanException("failed to allocate buffer memory!");

            vk.BindBufferMemory(device.Device, this.buffer, this.memory, 0);
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyBuffer(this.Device.Device, this.buffer, null);
            this.Vk.FreeMemory(this.Device.Device, this.memory, null);
        }
    }

    public void CopyTo(VulkanBuffer other, VulkanCommandPool commandPool)
    {
        unsafe
        {
            using VulkanCommandBuffer commandBuffer = new(this.Vk, this.Device, commandPool.CommandPool);

            BufferCopy copyRegion = new(0, 0, this.Size);
            this.Vk.CmdCopyBuffer(commandBuffer.Buffer, this.Buffer, other.Buffer, 1, in copyRegion);
        }
    }
}

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
