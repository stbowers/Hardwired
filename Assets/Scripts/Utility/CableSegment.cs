#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;

namespace Hardwired.Utility
{
    public class CableSegment
    {
        public Node NodeA { get; set; }

        public Node NodeB { get; set; }

        public List<Cable> Cables { get; } = new();

        public CableSegment(Node a, Node b)
        {
            NodeA = a;
            NodeB = b;
        }
    }
}