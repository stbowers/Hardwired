#nullable enable

using System;
using System.Reflection;

namespace Hardwired.Utility
{
    public static class ReflectionHelper
    {
        /// <summary>
        /// Gets a delegate which calls the implementation of a virtual method from `baseType`, rather than the derrived implementaiton.
        /// 
        /// This is intended to be used inside of a derrived class in order to call an ancestor's version of an overriden method, instead
        /// of the base class' version (i.e. `base.base.DoSomething()`).
        /// 
        /// Because inheritance is evil, and we need to find ways to work around it sometimes...
        /// </summary>
        public static TDelegate GetNonVirtualDelegate<TDelegate>(Type baseType, string methodName, object instance)
        {
            MethodInfo methodInfo = baseType.GetMethod(methodName);
            var fnPointer = methodInfo.MethodHandle.GetFunctionPointer();
            var fn = (TDelegate)Activator.CreateInstance(typeof(TDelegate), instance, fnPointer);

            return fn;
        }
    }
}