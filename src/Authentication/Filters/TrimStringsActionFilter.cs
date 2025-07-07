using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Altinn.Platform.Authentication.Attributes;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Platform.Authentication.Filters
{
    /// <summary>
    /// Action filter that trims whitespace from string properties of action arguments.
    /// </summary>
    public class TrimStringsActionFilter : IActionFilter
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

        /// <summary>
        /// Called before the action method is invoked.
        /// Trims whitespace from all string properties of the action arguments,
        /// except those marked with <see cref="DoNotTrimAttribute"/>.
        /// </summary>
        /// <param name="context">The action executing context.</param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument == null)
                {
                    continue;
                }

                // Recursively trim strings in the argument object
                TrimStrings(argument);
            }
        }

        /// <summary>
        /// Called after the action method is invoked.
        /// This implementation does nothing.
        /// </summary>
        /// <param name="context">The action executed context.</param>
        public void OnActionExecuted(ActionExecutedContext context) 
        {
        }

        /// <summary>
        /// Recursively trims whitespace from all string properties of the given object,
        /// except those marked with <see cref="DoNotTrimAttribute"/>.
        /// </summary>
        /// <param name="obj">The object whose string properties should be trimmed.</param>
        private void TrimStrings(object obj)
        {
            if (obj == null)
            {
                return;
            }

            var type = obj.GetType();
            if (type == typeof(string))
            {
                return;
            }

            // Get or add property metadata from the cache
            var properties = _propertyCache.GetOrAdd(type, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                 .ToArray());

            foreach (var prop in properties)
            {
                // Skip properties with [DoNotTrim]
                if (prop.GetCustomAttribute<DoNotTrimAttribute>() != null)
                {
                    continue;
                }

                if (prop.PropertyType == typeof(string))
                {
                    var value = (string?)prop.GetValue(obj);
                    if (value != null)
                    {
                        prop.SetValue(obj, value.Trim());
                    }
                }
                else if (!prop.PropertyType.IsPrimitive && !prop.PropertyType.IsEnum && !prop.PropertyType.IsArray)
                {
                    var nestedValue = prop.GetValue(obj);
                    if (nestedValue != null)
                    {
                        TrimStrings(nestedValue);
                    }
                }
            }
        }
    }
}
