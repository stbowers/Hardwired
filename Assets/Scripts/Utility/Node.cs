#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Hardwired.Objects.Electrical;

namespace Hardwired.Utility
{
    public class Node
    {
        public int? Index { get; set; }

        public List<Connection> Connections { get; } = new();
    }
}