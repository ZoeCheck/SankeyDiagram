﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kant.Wpf.Controls.Chart
{
    public class SankeyDiagramAssist
    {
        #region Constructor

        public SankeyDiagramAssist(SankeyDiagram diagram, SankeyStyleManager styleManager)
        {
            this.diagram = diagram;
            this.styleManager = styleManager;
        }

        #endregion

        #region Methods

        public void UpdateDiagram(IEnumerable<SankeyDataRow> datas)
        {
            // clear diagram
            if (!(diagram.DiagramCanvas == null || diagram.DiagramCanvas.Children == null || diagram.DiagramCanvas.Children.Count == 0))
            {
                RemoveElementEventHandlers();
                diagram.DiagramCanvas.Children.Clear();
                CurrentNodes.Clear();
                CurrentLabels.Clear();
                CurrentLinks.Clear();
                styleManager.ClearHighlight();
            }

            if (datas == null || datas.Count() == 0)
            {
                return;
            }

            CurrentLabels = new List<TextBlock>();
            styleManager.DefaultNodeLinksPaletteIndex = 0;

            // drawing...
            if (diagram.IsDiagramCreated)
            {
                CreateDiagram(datas);
            }
        }
        
        public void CreateDiagram(IEnumerable<SankeyDataRow> datas)
        {
            if (diagram.DiagramCanvas.ActualHeight <= 0 || diagram.DiagramCanvas.ActualWidth <= 0)
            {
                return;
            }

            #region preparing...

            var nodes = CreateNodes(datas);
            var panelLength = diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom ? diagram.DiagramCanvas.ActualWidth : diagram.DiagramCanvas.ActualHeight;
            var unitLength = panelLength;
            var maxNodeCountInOneLevel = 0;

            foreach (var levelNodes in CurrentNodes.Values)
            {
                if(levelNodes.Count > maxNodeCountInOneLevel)
                {
                    maxNodeCountInOneLevel = levelNodes.Count;
                }

                var sum = 0.0;

                if(diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                {
                    sum = levelNodes.Sum(node => node.Shape.Width);
                }
                else
                {
                    sum = levelNodes.Sum(node => node.Shape.Height);
                }

                var length = (panelLength - (levelNodes.Count - 1) * diagram.NodeGap) / sum;

                 if (length < unitLength)
                {
                    unitLength = length;
                }

                if (!(diagram.UsePallette == SankeyPalette.NodesLinks || diagram.NodeBrushes != null))
                {
                    foreach (var node in levelNodes)
                    {
                        node.Shape.Fill = diagram.NodeBrush.CloneCurrentValue();
                    }
                }
            }

            // 15 means you have to remain some margin to calculate node's position, or a wrong position of the top node
            var nodesOverallLength = panelLength - (maxNodeCountInOneLevel * diagram.NodeGap) - 15;

            if (nodesOverallLength <= 0)
            {
                diagram.DiagramCanvas.Children.Add(new TextBlock() { Text = "diagram panel length is not enough" });

                return;
            }

            var labelContainers = new List<Canvas>();
            var relaxation = new SankeyIterativeRelaxation();
            CurrentNodes = relaxation.Calculate(diagram.SankeyFlowDirection, CurrentNodes, CurrentLinks, panelLength, diagram.NodeGap, unitLength, 32);

            #endregion
 
            #region add nodes & links & labels

            foreach (var node in nodes)
            {
                Canvas.SetTop(node.Shape, node.Y);
                Canvas.SetLeft(node.Shape, node.X);
                diagram.DiagramCanvas.Children.Add(node.Shape);

                if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                {
                    node.OutLinks.Sort((l1, l2) => { return (int)(l1.ToNode.X - l2.ToNode.X); });
                    node.InLinks.Sort((l1, l2) => { return (int)(l1.FromNode.X - l2.FromNode.X); });
                }
                else
                {
                    node.OutLinks.Sort((l1, l2) => { return (int)(l1.ToNode.Y - l2.ToNode.Y); });
                    node.InLinks.Sort((l1, l2) => { return (int)(l1.FromNode.Y - l2.FromNode.Y); });
                }

                var fromPosition = 0.0;
                var toPosition = 0.0;

                foreach (var outLink in node.OutLinks)
                {
                    outLink.FromPosition = fromPosition;
                    outLink.Width = outLink.Weight * unitLength;
                    //outLink.Shape.StrokeThickness = outLink.Weight * unitLength;
                    //fromPosition += outLink.Shape.StrokeThickness;
                    fromPosition += outLink.Width;
                }

                foreach (var inLink in node.InLinks)
                {
                    inLink.ToPosition = toPosition;
                    inLink.Width = inLink.Weight * unitLength;
                    //inLink.Shape.StrokeThickness = inLink.Weight * unitLength;
                    //toPosition += inLink.Shape.StrokeThickness;
                    toPosition += inLink.Width;
                }
            }

            foreach(var link in CurrentLinks)
            {
                // make link under the node
                Panel.SetZIndex(link.Shape, -1);
                diagram.DiagramCanvas.Children.Add(DrawLink(link).Shape);
            }

            var needAddLabels = CurrentLabels.Count == 0 ? true : false;

            if (needAddLabels)
            {
                styleManager.OriginalLabelOpacity = this.CurrentNodes[0][0].Label.Opacity;

                for(var index = 0; index < CurrentNodes.Count; index++)
                {
                    foreach(var node in CurrentNodes[index])
                    {
                        node.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                        if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                        {
                            Canvas.SetLeft(node.Label, node.X + (node.Shape.Width / 2) - (node.Label.DesiredSize.Width / 2));

                            if (index == nodes.Count - 1)
                            {
                                Canvas.SetBottom(node.Label, node.Shape.Height);
                            }
                            else
                            {
                                Canvas.SetTop(node.Label, node.Y + node.Shape.Height);
                            }
                        }
                        else
                        {
                            Canvas.SetTop(node.Label, node.Y + (node.Shape.Height / 2) - (node.Label.DesiredSize.Height / 2));

                            if (index == CurrentNodes.Count - 1)
                            {
                                Canvas.SetRight(node.Label, node.Shape.Width);
                            }
                            else
                            {
                                Canvas.SetLeft(node.Label, node.X + node.Shape.Width);
                            }
                        }

                        CurrentLabels.Add(node.Label);
                        diagram.DiagramCanvas.Children.Add(node.Label);
                    }
                }
            }

            styleManager.ChangeLabelsVisibility(diagram.ShowLabels, CurrentLabels);

            #endregion
        }

        private List<SankeyNode> CreateNodes(IEnumerable<SankeyDataRow> datas)
        {
            var nodes = new List<SankeyNode>();
            var links = new List<SankeyLink>();
            styleManager.DefaultNodeLinksPaletteIndex = 0;

            foreach(var data in datas)
            {
                if(!nodes.Exists(n => n.Name == data.From))
                {
                    nodes.Add(CreateNode(data, data.From));
                }

                if (!nodes.Exists(n => n.Name == data.To))
                {
                    nodes.Add(CreateNode(data, data.To));
                }

                var fromNode = nodes.Find(findNode => findNode.Name == data.From);

                if(fromNode != null)
                {
                    var toNode = nodes.Find(findNode => findNode.Name == data.To);

                    if(toNode != null)
                    {
                        // merge links which has the same from & to
                        if (links != null)
                        {
                            var previousLink = links.Find(findLink => findLink.FromNode.Name == fromNode.Name && findLink.ToNode.Name == toNode.Name);

                            if (previousLink != null)
                            {
                                previousLink.Weight += data.Weight;

                                continue;
                            }
                        }

                        // create link 
                        var shape = new Path();
                        shape.MouseEnter += LinkMouseEnter;
                        shape.MouseLeave += LinkMouseLeave;
                        shape.MouseLeftButtonUp += LinkMouseLeftButtonUp;
                        shape.Tag = new SankeyLinkFinder(data.From, data.To);
                        shape.Fill = diagram.UsePallette != SankeyPalette.None ? fromNode.Shape.Fill.CloneCurrentValue() : data.LinkBrush == null ? styleManager.DefaultLinkBrush.CloneCurrentValue() : data.LinkBrush.CloneCurrentValue();
                        var link = new SankeyLink(fromNode, toNode, shape, data.Weight, shape.Fill.CloneCurrentValue());
                        fromNode.OutLinks.Add(link);
                        toNode.InLinks.Add(link);
                        links.Add(link);
                    }
                }
            }

            CurrentLinks = links;
            CurrentNodes = CalculateNodeLevel(nodes, diagram.NodeThickness);

            return nodes;
        }

        private Dictionary<int, List<SankeyNode>> CalculateNodeLevel(List<SankeyNode> nodes, double nodeThickness)
        {
            var tempNodes = new Dictionary<double, List<SankeyNode>>();
            var remainNodes = nodes;
            var nextNodes = new List<SankeyNode>();
            var length = 0.0;
            var levelIndex = 0;
            var linkLength = 0.0;

            if(diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
            {
                length = diagram.DiagramCanvas.ActualHeight;
            }
            else
            {
                length = diagram.DiagramCanvas.ActualWidth;
            }

            while(remainNodes.Count > 0)
            {
                nextNodes = new List<SankeyNode>();
                var nodeIndex = 0;
                var nodeCount = remainNodes.Count;

                for(; nodeIndex < nodeCount; nodeIndex++)
                {
                    var node = remainNodes[nodeIndex];

                    if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                    {
                        node.Y = levelIndex;
                    }
                    else
                    {
                        node.X = levelIndex;
                    }

                    var linkIndex = 0;
                    var linkCount = node.OutLinks.Count;

                    for(; linkIndex < linkCount; linkIndex++)
                    {
                        nextNodes.Add(node.OutLinks[linkIndex].ToNode);
                    }
                }

                remainNodes = nextNodes;
                levelIndex++;
            }

            // move all the node without outLinks to end
            foreach(var node in nodes)
            {
                if(node.OutLinks.Count == 0)
                {
                    if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                    {
                        node.Y = levelIndex - 1;
                    }
                    else
                    {
                        node.X = levelIndex - 1;
                    }
                }
            }

            linkLength = (length - nodeThickness) / (levelIndex - 1);

            foreach(var node in nodes)
            {
                var max = Math.Max(node.InLinks.Sum(l => l.Weight), node.OutLinks.Sum(l => l.Weight));

                if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
                {
                    if (tempNodes.Keys.Contains(node.Y))
                    {
                        tempNodes[node.Y].Add(node);
                    }
                    else
                    {
                        tempNodes.Add(node.Y, new List<SankeyNode>() { node });
                    }

                    node.Y *= linkLength;
                    node.Shape.Width = max;
                }
                else
                {
                    if (tempNodes.Keys.Contains(node.X))
                    {
                        tempNodes[node.X].Add(node);
                    }
                    else
                    {
                        tempNodes.Add(node.X, new List<SankeyNode>() { node });
                    }

                    node.X *= linkLength;
                    node.Shape.Height = max;
                }
            }

            var nodesByLevel = tempNodes.OrderBy(n => n.Key).ToDictionary(item => (int)item.Key, item => item.Value);

            return nodesByLevel;
        }

        private SankeyNode CreateNode(SankeyDataRow data, string name)
        {
            var text = new TextBlock()
            {
                Text = name,
                Style = diagram.LabelStyle
            };

            var shape = new Rectangle();
            shape.Tag = name;

            // for highlighting or other actions
            shape.MouseEnter += NodeMouseEnter;
            shape.MouseLeave += NodeMouseLeave;
            shape.MouseLeftButtonUp += NodeMouseLeftButtonUp;

            if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
            {
                shape.Height = diagram.NodeThickness;
            }
            else
            {
                shape.Width = diagram.NodeThickness;
            }

            var node = new SankeyNode(shape, text);
            SetNodeBrush(node);
            node.Name = name;

            return node;
        }

        private void SetNodeBrush(SankeyNode node)
        {
            var brushCheck = diagram.NodeBrushes != null && diagram.NodeBrushes.Keys.Contains(node.Label.Text);

            if (brushCheck)
            {
                node.Shape.Fill = diagram.NodeBrushes[node.Label.Text].CloneCurrentValue();
            }
            else
            {
                if (diagram.UsePallette != SankeyPalette.None)
                {
                    node.Shape.Fill = styleManager.DefaultNodeLinksPalette[styleManager.DefaultNodeLinksPaletteIndex].CloneCurrentValue();
                    styleManager.DefaultNodeLinksPaletteIndex++;

                    if (styleManager.DefaultNodeLinksPaletteIndex >= styleManager.DefaultNodeLinksPalette.Count)
                    {
                        styleManager.DefaultNodeLinksPaletteIndex = 0;
                    }
                }
            }

            node.OriginalShapBrush = node.Shape.Fill.CloneCurrentValue();
        }

        private void LinkMouseEnter(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseEnter)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightLinkProperty, (SankeyLinkFinder)((Path)e.OriginalSource).Tag);
            }
        }

        private void LinkMouseLeave(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseEnter)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightLinkProperty, (SankeyLinkFinder)((Path)e.OriginalSource).Tag);
            }
        }

        private void LinkMouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseLeftButtonUp)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightLinkProperty, (SankeyLinkFinder)((Path)e.OriginalSource).Tag);
            }
        }

        private void NodeMouseEnter(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseEnter)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightNodeProperty, ((Rectangle)e.OriginalSource).Tag as string);
            }
        }

        private void NodeMouseLeave(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseEnter)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightNodeProperty, ((Rectangle)e.OriginalSource).Tag as string);
            }
        }

        private void NodeMouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            if (diagram.HighlightMode == SankeyHighlightMode.MouseLeftButtonUp)
            {
                diagram.SetCurrentValue(SankeyDiagram.HighlightNodeProperty, ((Rectangle)e.OriginalSource).Tag as string);
            }
        }

        private SankeyLink DrawLink(SankeyLink link)
        {
            var fromPoint = new Point();
            var toPoint = new Point();
            var bezierControlPoint1 = new Point();
            var bezierControlPoint2 = new Point();
            var fromPoint2 = new Point();
            var toPoint2 = new Point();
            var bezier2ControlPoint1 = new Point();
            var bezier2COntrolPoint2 = new Point();

            if (diagram.SankeyFlowDirection == SankeyFlowDirection.TopToBottom)
            {
                //fromPoint.X = link.FromNode.X + link.FromPosition + link.Shape.StrokeThickness / 2;
                //fromPoint.Y = link.FromNode.Y + link.FromNode.Shape.Height;
                //toPoint.X = link.ToNode.X + link.ToPosition + link.Shape.StrokeThickness / 2;
                //toPoint.Y = link.ToNode.Y;
                fromPoint.X = link.FromNode.X + link.FromPosition;
                fromPoint2.X = fromPoint.X + link.Width;
                fromPoint2.Y = fromPoint.Y = link.FromNode.Y + link.FromNode.Shape.Height;
                toPoint.X = link.ToNode.X + link.ToPosition;
                toPoint2.X = toPoint.X + link.Width;
                toPoint2.Y = toPoint.Y = link.ToNode.Y;
                var length = toPoint.Y - fromPoint.Y;
                //bezierControlPoint1.X = fromPoint.X;
                //bezierControlPoint1.Y = length * diagram.LinkCurvature + fromPoint.Y;
                //bezierCOntrolPoint2.X = toPoint.X;
                //bezierCOntrolPoint2.Y = length * (1 - diagram.LinkCurvature) + fromPoint.Y;
                bezierControlPoint1.X = fromPoint.X;
                bezier2ControlPoint1.X = fromPoint2.X;
                bezier2ControlPoint1.Y = bezierControlPoint1.Y = length * diagram.LinkCurvature + fromPoint.Y;
                bezierControlPoint2.X = toPoint.X;
                bezier2COntrolPoint2.X = toPoint2.X;
                bezier2COntrolPoint2.Y = bezierControlPoint2.Y = length * (1 - diagram.LinkCurvature) + fromPoint.Y;
            }
            else
            {
                fromPoint.Y = link.FromNode.Y + link.FromPosition + link.Shape.StrokeThickness / 2;
                fromPoint.X = link.FromNode.X + link.FromNode.Shape.Width;
                toPoint.Y = link.ToNode.Y + link.ToPosition + link.Shape.StrokeThickness / 2;
                toPoint.X = link.ToNode.X;
                var length = toPoint.X - fromPoint.X;
                bezierControlPoint1.Y = fromPoint.Y;
                bezierControlPoint1.X = length * diagram.LinkCurvature + fromPoint.X;
                bezierControlPoint2.Y = toPoint.Y;
                bezierControlPoint2.X = length * (1 - diagram.LinkCurvature) + fromPoint.X;
            }

            var geometry = new PathGeometry()
            {
                Figures = new PathFigureCollection()
                {
                    new PathFigure()
                    {
                        StartPoint = fromPoint,

                        Segments = new PathSegmentCollection()
                        {
                            new LineSegment() { },

                            new BezierSegment()
                            {
                                Point1 = bezierControlPoint1,
                                Point2 = bezierControlPoint2,
                                Point3 = toPoint
                            },

                            new LineSegment() { },

                            new BezierSegment()
                            {

                            }
                        }
                    },
                }
            };

            link.Shape.Data = geometry;

            return link;
        }

        private void RemoveElementEventHandlers()
        {
            if (CurrentLinks != null && CurrentNodes != null)
            {
                foreach(var levelNodes in CurrentNodes.Values)
                {
                    foreach(var node in levelNodes)
                    {
                        node.Shape.MouseEnter -= NodeMouseEnter;
                        node.Shape.MouseLeave -= NodeMouseLeave;
                        node.Shape.MouseLeftButtonUp -= NodeMouseLeftButtonUp;
                    }
                }

                foreach (var link in CurrentLinks)
                {
                    link.Shape.MouseEnter -= LinkMouseEnter;
                    link.Shape.MouseLeave -= LinkMouseLeave;
                    link.Shape.MouseLeftButtonUp -= LinkMouseLeftButtonUp;
                }
            }
        }

        #endregion

        #region Fields & Properties

        public List<TextBlock> CurrentLabels { get; private set; }

        /// <summary>
        /// key means depth
        /// </summary>
        public Dictionary<int, List<SankeyNode>> CurrentNodes { get; private set; }

        public List<SankeyLink> CurrentLinks { get; set; }

        private SankeyDiagram diagram;

        private SankeyStyleManager styleManager;

        #endregion
    }
}
