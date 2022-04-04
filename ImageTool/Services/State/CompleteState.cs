﻿using System.Windows;

namespace ImageTool.Services.State
{
    internal class CompleteState : IToolState
    {
        public void OnMouseDown(Point point)
        {
        }

        public Position? OnMouseMove(Point point)
        {
            return null;
        }

        public void OnMouseUp(Point point)
        {
        }
    }
}
