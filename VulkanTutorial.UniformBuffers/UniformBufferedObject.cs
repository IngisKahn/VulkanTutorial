﻿using Silk.NET.Maths;

namespace VulkanTutorial.UniformBuffers;

public struct UniformBufferedObject
    {
        public Matrix4X4<float> Model;
        public Matrix4X4<float> View;
        public Matrix4X4<float> Projection;
    }
