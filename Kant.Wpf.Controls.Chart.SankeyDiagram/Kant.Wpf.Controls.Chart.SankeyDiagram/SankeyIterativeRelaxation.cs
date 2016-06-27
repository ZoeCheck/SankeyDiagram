﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kant.Wpf.Controls.Chart
{
    public class SankeyIterativeRelaxation
    {
        public Dictionary<int, List<SankeyNode>> Calculate(SankeyFlowDirection flowDirection, Dictionary<int, List<SankeyNode>> nodes, List<SankeyLink> links, double panelLength, double nodeGap, double unitLength, int iterations)
        {
            nodes = InitializeNodeLength(nodes, unitLength, flowDirection);
            nodes = ResolveCollisions(nodes, panelLength, nodeGap, flowDirection);
            var relaxationAlpha = 1.0;

            for (; iterations > 0; iterations--)
            {
                relaxationAlpha *= 0.99;
                nodes = RelaxFromEndToFront(nodes, relaxationAlpha, flowDirection);
                nodes = ResolveCollisions(nodes, panelLength, nodeGap, flowDirection);
                nodes = RelaxFromFrontToEnd(nodes, relaxationAlpha, flowDirection);
                nodes = ResolveCollisions(nodes, panelLength, nodeGap, flowDirection);
            }

            return nodes;
        }

        private Dictionary<int, List<SankeyNode>> InitializeNodeLength(Dictionary<int, List<SankeyNode>> nodes, double unitLength, SankeyFlowDirection flowDirection)
        {
            foreach (var levelNodes in nodes.Values)
            {
                var index = 0;

                foreach (var node in levelNodes)
                {
                    if (flowDirection == SankeyFlowDirection.TopToBottom)
                    {
                        node.Shape.Width *= unitLength;
                        node.Position2 = index;
                    }
                    else
                    {
                        node.Shape.Height *= unitLength;
                        node.Position1 = index;
                    }

                    index++;
                }
            }

            return nodes;
        }

        private Dictionary<int, List<SankeyNode>> ResolveCollisions(Dictionary<int, List<SankeyNode>> nodes, double panelLength, double nodeGap, SankeyFlowDirection flowDirection)
        {
            foreach (var levelNodes in nodes.Values)
            {
                var tempValue1 = 0.0;
                var tempValue2 = 0.0;

                if (flowDirection == SankeyFlowDirection.TopToBottom)
                {
                    levelNodes.Sort((n1, n2) => { return (int)(n1.Position2 - n2.Position2); });
                }
                else
                {
                    levelNodes.Sort((n1, n2) => { return (int)(n1.Position1 - n2.Position1); });
                }

                foreach (var node in levelNodes)
                {
                    if (flowDirection == SankeyFlowDirection.TopToBottom)
                    {
                        tempValue1 = tempValue2 - node.Position2;

                        if (tempValue1 > 0)
                        {
                            node.Position2 += tempValue1;
                        }

                        tempValue2 = node.Position2 + node.Shape.Width + nodeGap;
                    }
                    else
                    {
                        tempValue1 = tempValue2 - node.Position1;

                        if (tempValue1 > 0)
                        {
                            node.Position1 += tempValue1;
                        }

                        tempValue2 = node.Position1 + node.Shape.Height + nodeGap;
                    }
                }

                // if the last node goes outside the panel, push it back up
                tempValue1 = tempValue2 - nodeGap - panelLength;

                if (tempValue1 > 0)
                {
                    if (flowDirection == SankeyFlowDirection.TopToBottom)
                    {
                        tempValue2 = levelNodes.Last().Position2 -= tempValue1;
                    }
                    else
                    {
                        tempValue2 = levelNodes.Last().Position1 -= tempValue1;
                    }

                    for (var index = levelNodes.Count - 2; index >= 0; index--)
                    {
                        var node = levelNodes[index];

                        if (flowDirection == SankeyFlowDirection.TopToBottom)
                        {
                            tempValue1 = node.Position2 + node.Shape.Width + nodeGap - tempValue2;

                            if (tempValue1 > 0)
                            {
                                node.Position2 -= tempValue1;
                            }

                            tempValue2 = node.Position2;
                        }
                        else
                        {
                            tempValue1 = node.Position1 + node.Shape.Height + nodeGap - tempValue2;

                            if(tempValue1 > 0)
                            {
                                node.Position1 -= tempValue1;
                            }

                            tempValue2 = node.Position1;
                        }
                    }
                }
            }

            return nodes;
        }

        private Dictionary<int, List<SankeyNode>> RelaxFromFrontToEnd(Dictionary<int, List<SankeyNode>> nodes, double alpha, SankeyFlowDirection flowDirection)
        {
            foreach (var levelNodes in nodes.Values)
            {
                foreach (var node in levelNodes)
                {
                    if (node.InLinks.Count > 0)
                    {
                        var tempValue = node.InLinks.Sum(link => GetCenterValue(link.FromNode, flowDirection) * link.Weight) / node.InLinks.Sum(link => link.Weight);

                        if (flowDirection == SankeyFlowDirection.TopToBottom)
                        {
                            node.Position2 += (tempValue - GetCenterValue(node, flowDirection)) * alpha;
                        }
                        else
                        {
                            node.Position1 += (tempValue - GetCenterValue(node, flowDirection)) * alpha;
                        }
                    }
                }
            }

            return nodes;
        }

        private Dictionary<int, List<SankeyNode>> RelaxFromEndToFront(Dictionary<int, List<SankeyNode>> nodes, double alpha, SankeyFlowDirection flowDirection)
        {
            for (var index = nodes.Count - 1; index >= 0; index--)
            {
                foreach (var node in nodes[index])
                {
                    if (node.OutLinks.Count > 0)
                    {
                        var tempValue = node.OutLinks.Sum(link => GetCenterValue(link.ToNode, flowDirection) * link.Weight) / node.OutLinks.Sum(link => link.Weight);

                        if (flowDirection == SankeyFlowDirection.TopToBottom)
                        {
                            node.Position2 += (tempValue - GetCenterValue(node, flowDirection)) * alpha;
                        }
                        else
                        {
                            node.Position1 += (tempValue - GetCenterValue(node, flowDirection)) * alpha;
                        }
                    }
                }
            }

            return nodes;
        }

        private double GetCenterValue(SankeyNode node, SankeyFlowDirection flowDirection)
        {
            return flowDirection == SankeyFlowDirection.TopToBottom ? node.Position2 + node.Shape.Width / 2 : node.Position1 + node.Shape.Height / 2;
        }
    }
}
