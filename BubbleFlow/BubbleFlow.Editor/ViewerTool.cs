﻿using Dreambuild.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BubbleFlow.Editor
{
    public class WheelScalingTool : ViewerTool
    {
        private static readonly double[] ZoomLevels = new double[] { 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125, 0.0625, 0.03125, 0.015625 };

        public override void MouseWheelHandler(object sender, MouseWheelEventArgs e)
        {
            base.MouseWheelHandler(sender, e);

            var basePoint = e.GetPosition(MainWindow.Current);
            int index = WheelScalingTool.FindScaleIndex(MainWindow.Current.Scale);
            index += e.Delta / 120;
            if (index > ZoomLevels.Length - 1)
            {
                index = ZoomLevels.Length - 1;
            }
            else if (index < 0)
            {
                index = 0;
            }

            double scale = ZoomLevels[index];
            MainWindow.Current.ScaleCanvas(scale, basePoint); // TODO: look into ScaleCanvas().
        }

        private static int FindScaleIndex(double scale)
        {
            for (int i = 0; i < ZoomLevels.Length; i++)
            {
                if (scale > ZoomLevels[i] * 0.75)
                {
                    return i;
                }
            }

            return ZoomLevels.Length - 1;
        }
    }

    public class PanCanvasTool : ViewerTool
    {
        private Point PreviousPosition;

        public override void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(MainWindow.Current);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                MainWindow.Current.PanCanvas(position - this.PreviousPosition);
            }

            this.PreviousPosition = position;
        }

        public override void EnterToolHandler()
        {
            base.EnterToolHandler();

            ViewerToolManager.AddTool(new SelectNodeTool());
            ViewerToolManager.AddTool(new EditNodeTool());
        }

        public override void ExitToolHandler()
        {
            base.ExitToolHandler();

            var tools = ViewerToolManager.Tools.ToList();
            tools.ForEach(tool =>
            {
                if (tool is SelectNodeTool || tool is EditNodeTool)
                {
                    ViewerToolManager.RemoveTool(tool);
                }
            });
        }
    }

    public class AddNodeTool : ViewerTool
    {
        private readonly NodeBubble PhantomBubble = new NodeBubble();

        public override void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(MainWindow.Current.MyCanvas);
            if (MainWindow.Current.GridToggleSwitch.IsChecked == true)
            {
                position = MainWindow.Current.GridLines.Snap(position);
            }

            this.PhantomBubble.Position = position;
            this.PhantomBubble.ReadyControl();
        }

        public override void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var clickPoint = e.GetPosition(MainWindow.Current.MyCanvas);
                if (MainWindow.Current.GridToggleSwitch.IsChecked == true)
                {
                    clickPoint = MainWindow.Current.GridLines.Snap(clickPoint);
                }

                var node = new FlowNode
                {
                    ID = Guid.NewGuid(),
                    Name = "New node",
                    Metadata = new FlowElementMetadata
                    {
                        Left = clickPoint.X,
                        Top = clickPoint.Y
                    },
                    Properties = new JObject
                    {
                        { "role", string.Empty },
                        { "user", string.Empty }
                    }
                };

                var bubble = new NodeBubble
                {
                    NodeID = node.ID,
                    Text = node.Name,
                    Position = clickPoint
                };

                Canvas.SetZIndex(bubble, 100);
                bubble.ReadyControl();

                MainWindow.Current.MyCanvas.Children.Add(bubble);
                MainWindow.Current.Bubbles.Add(node.ID, bubble);
                DataManager.CurrentDocument.NodesStore.Add(node.ID, node);
            }
        }

        public override void EnterToolHandler()
        {
            base.EnterToolHandler();

            MainWindow.Current.MyCanvas.Children.Add(this.PhantomBubble);
        }

        public override void ExitToolHandler()
        {
            base.ExitToolHandler();

            MainWindow.Current.MyCanvas.Children.Remove(this.PhantomBubble);
        }
    }

    public class AddLinkTool : ViewerTool
    {
        private NodeBubble StartNode;
        private NodeBubble EndNode;
        private int ClickCount = 0;

        private readonly BezierLink PhantomArrow = new BezierLink();

        public override void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (this.StartNode != null)
            {
                this.PhantomArrow.EndPoint = e.GetPosition(MainWindow.Current.MyCanvas);
                this.PhantomArrow.ReadyControl();
            }
        }

        public override void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is FrameworkElement element)
                {
                    if (element.Parent is NodeBubble bubble)
                    {
                        this.ClickCount++;

                        if (this.ClickCount == 1)
                        {
                            this.StartNode = bubble;
                            this.PhantomArrow.StartPoint = bubble.Position;
                            MainWindow.Current.MyCanvas.Children.Add(this.PhantomArrow);
                        }
                        else
                        {
                            if (bubble != this.StartNode)
                            {
                                this.EndNode = bubble;
                            }
                        }
                    }
                }

                // TODO: use status bar to show this info.
            }
        }

        public override void MouseUpHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (this.ClickCount >= 2)
                {
                    if (this.StartNode != null && this.EndNode != null)
                    {
                        if (!MainWindow.Current.Arrows.ContainsKeys(this.StartNode.NodeID, this.EndNode.NodeID))
                        {
                            var link = new FlowLink
                            {
                                From = this.StartNode.NodeID,
                                To = this.EndNode.NodeID,
                                Label = string.Empty
                            };

                            var arrow = new BezierLink
                            {
                                StartPoint = this.StartNode.Position,
                                EndPoint = this.EndNode.Position,
                                StartOffset = DataManager.NodeSize / 2,
                                EndOffset = DataManager.NodeSize / 2,
                                Tag = (link.From, link.To)
                            };

                            Canvas.SetZIndex(arrow, 100);
                            arrow.ReadyControl();

                            MainWindow.Current.MyCanvas.Children.Add(arrow);
                            MainWindow.Current.Arrows.Add(link.From, link.To, arrow);
                            DataManager.CurrentDocument.LinksStore.Add(link.From, link.To, link);
                        }

                        this.ClickCount = 0;
                        this.StartNode = null;
                        this.EndNode = null;
                        MainWindow.Current.MyCanvas.Children.Remove(this.PhantomArrow);
                    }
                }
            }
        }

        public override void ExitToolHandler()
        {
            base.ExitToolHandler();

            MainWindow.Current.MyCanvas.Children.Remove(this.PhantomArrow);
        }
    }

    public class MoveNodeTool : ViewerTool
    {
        private Point PreviousPosition;
        private NodeBubble BubbleToMove;

        public override void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(MainWindow.Current.MyCanvas);
            if (e.LeftButton == MouseButtonState.Pressed && this.BubbleToMove != null)
            {
                this.BubbleToMove.Position += position - this.PreviousPosition;
                this.BubbleToMove.ReadyControl();
                this.UpdateAllLinks(position);
            }

            this.PreviousPosition = position;
        }

        public override void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is FrameworkElement element)
                {
                    if (element.Parent is NodeBubble bubble)
                    {
                        this.BubbleToMove = bubble;
                    }
                }
            }
        }

        public override void MouseUpHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (this.BubbleToMove != null)
                {
                    if (MainWindow.Current.GridToggleSwitch.IsChecked == true)
                    {
                        this.BubbleToMove.Position = MainWindow.Current.GridLines.Snap(this.BubbleToMove.Position);
                        this.BubbleToMove.ReadyControl();
                        this.UpdateAllLinks(this.BubbleToMove.Position);
                    }

                    var node = DataManager.CurrentDocument.NodesStore[this.BubbleToMove.NodeID];
                    node.Metadata.Left = this.BubbleToMove.Position.X;
                    node.Metadata.Top = this.BubbleToMove.Position.Y;
                }

                this.BubbleToMove = null;
            }
        }

        private void UpdateAllLinks(Point position)
        {
            MainWindow.Current.Arrows.RealValues.ForEach(arrow =>
            {
                if (arrow.FromNodeID == this.BubbleToMove.NodeID)
                {
                    arrow.StartPoint = position;
                    arrow.ReadyControl();
                }
                else if (arrow.ToNodeID == this.BubbleToMove.NodeID)
                {
                    arrow.EndPoint = position;
                    arrow.ReadyControl();
                }
            });
        }
    }

    public class SelectNodeTool : ViewerTool
    {
        // TODO: move the colors to Node class when CanvasIdentifiableElement base class is done.
        private static readonly Color DefaultColor = Colors.DarkGray;
        private static readonly Color HighlightColor = Colors.Orange;

        public override void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (MainWindow.Current.SelectedBubble != null)
                {
                    MainWindow.Current.SelectedBubble.FillColor = DefaultColor;
                    MainWindow.Current.SelectedBubble.ReadyControl();
                }

                if (e.OriginalSource is FrameworkElement element)
                {
                    if (element.Parent is NodeBubble bubble)
                    {
                        MainWindow.Current.SelectedBubble = bubble;
                        bubble.FillColor = HighlightColor;
                        bubble.ReadyControl();

                        return;
                    }
                }

                MainWindow.Current.SelectedBubble = null;
            }
        }
    }

    public class EditNodeTool : ViewerTool
    {
        public override void MouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var bubble = MainWindow.Current.SelectedBubble;
                var node = DataManager.CurrentDocument.NodesStore[bubble.NodeID];

                var inputs = new Dictionary<string, string>
                {
                    { "Name", node.Name },
                    { "Role", node.Properties["role"].ToObject<string>() },
                    { "User", node.Properties["user"].ToObject<string>() }
                };

                Gui.MultiInputs("Node info", inputs);

                node.Name = inputs["Name"];
                node.Properties["role"] = inputs["Role"];
                node.Properties["user"] = inputs["User"];

                bubble.Text = inputs["Name"];
                bubble.ReadyControl();
            }
        }
    }

    public class EditLinkTool : ViewerTool
    {
        // TODO: don't want to look into this mess. Will revamp it using direct link pick rather than nodes pick.
    }
}
