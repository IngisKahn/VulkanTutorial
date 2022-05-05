using Silk.NET.Vulkan;

namespace VulkanTutorial.UniformBuffers;

public abstract class VulkanDependancy
{
    protected Vk Vk { get; }
    protected VulkanDependancy(Vk vk) => this.Vk = vk;
}
