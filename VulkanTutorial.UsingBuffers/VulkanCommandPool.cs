using Silk.NET.Vulkan;

namespace VulkanTutorial.UsingBuffers;

public sealed class VulkanCommandPool : VulkanDeviceDependancy, IDisposable
{
    private readonly CommandPool commandPool;
    public CommandPool CommandPool => this.commandPool;
    public VulkanCommandPool(Vk vk, VulkanPhysicalDevice physicalDevice, VulkanVirtualDevice device) : base(vk, device)
    {
        var queueFamilyIndices = physicalDevice.FindQueueFamilies();

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value
        };

        unsafe
        {
            fixed (CommandPool* pCommandPool = &this.commandPool)
                if (vk.CreateCommandPool(device.Device, &poolInfo, null, pCommandPool) != Result.Success)
                    throw new Exception("failed to create command pool!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyCommandPool(this.Device.Device, this.commandPool, null);
        }
    }
}