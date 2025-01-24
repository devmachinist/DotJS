using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using static DotJS.JS;

namespace DotJS
{
    public static class Invoker
    {
        public static JSReturn Run(Invocation invocation)
        {
            var timer = new Stopwatch();
            timer.Start();
            JSReturn jsReturn = new JSReturn
            {
                Instance = invocation.Instance
            };

            try
            {
                object? result = null;

                if (invocation.Ref != null)
                {
                    // Instance method invocation
                    result = InvokeNestedMethod(invocation.Ref, invocation.Method, invocation.Args);
                }
                else
                {
                    // Static method invocation
                    var methodParts = invocation.Method.Split('.');
                    if (methodParts.Length < 2)
                    {
                        throw new InvalidOperationException("Method must be in the format 'ClassName.MethodName' or have deeper nesting.");
                    }

                    string className = methodParts[0];
                    string[] methodChain = methodParts.Skip(1).ToArray();
                    var baseAssembly = Assembly.GetEntryAssembly();

                    // Get the type by searching all loaded assemblies
                    Type? type = baseAssembly.GetType(className);

                    if (type == null)
                    {
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            try
                            {
                                if (type != null) break;
                                var xas = assembly;
                                if (assembly.GetName().Name.Contains("Xavier")) 
                                {
                                    xas = Assembly.Load(xas.GetName().Name);
                                }

                                type = type ?? xas.GetType(className);

                            }
                            catch (ReflectionTypeLoadException ex)
                            {
                            }
                            catch (FileNotFoundException ex)
                            {
                            }
                        }

                        if(type == null) throw new InvalidOperationException($"Type '{className}' not found in any loaded assemblies.");
                    }
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { 
                        type = type ?? assembly.GetTypes().FirstOrDefault(t=> t.Name == className);
                    }

                    if (type == null)
                    {
                        throw new InvalidOperationException($"Type '{className}' not found in any loaded assemblies.");
                    }

                    result = InvokeNestedMethod(null, string.Join('.', methodChain), invocation.Args, type);
                }

                jsReturn.Value = result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                jsReturn.Value = $"Error: {ex.ToString()}";
            }
            timer.Stop();

            return jsReturn;
        }

        public static async Task<JSReturn> RunAsync(Invocation invocation)
        {
            var timer = new Stopwatch();
            timer.Start();
            JSReturn jsReturn = new JSReturn
            {
                Instance = invocation.Instance
            };

            try
            {
                object? result = null;

                if (invocation.Ref != null)
                {
                    // Instance method invocation
                    result = await InvokeNestedMethodAsync(invocation.Ref, invocation.Method, invocation.Args);
                }
                else
                {
                    // Static method invocation
                    var methodParts = invocation.Method.Split('.');
                    if (methodParts.Length < 2)
                    {
                        throw new InvalidOperationException("Method must be in the format 'ClassName.MethodName' or have deeper nesting.");
                    }
                    string className = string.Join(".",methodParts.Take(methodParts.Length - 1));
                    string[] methodChain = methodParts.Skip(methodParts.Length - 1).ToArray();
                    var baseAssembly = Assembly.GetEntryAssembly();

                    // Get the type by searching all loaded assemblies
                    Type? type = baseAssembly.GetType(className);

                    if (type == null)
                    {
                        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Contains("Xavier"));
                                type = type ?? assembly.GetType(className);


                        if(type == null) throw new InvalidOperationException($"Type '{className}' not found in any loaded assemblies.");
                    }
                    result = await InvokeNestedMethodAsync(null, string.Join('.', methodChain), invocation.Args, type);
                }

                jsReturn.Value = result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                jsReturn.Value = $"Error {ex.ToString()}";
            }
            timer.Stop();

            return jsReturn;
        }

        private static object? InvokeNestedMethod(object? obj, string methodChain, object[]? args, Type? type = null)
        {
            var methodParts = methodChain.Split('.');

            foreach (var part in methodParts.Take(methodParts.Length - 1))
            {
                var prop = ((obj is null) ? type : obj.GetType()).GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException($"Property '{part}' not found.");

                obj = prop.GetValue(obj)
                       ?? throw new InvalidOperationException($"Property '{part}' is null.");
            }
            var finalMethod = (methodParts.Length > 1) ? methodParts.Last() : methodParts[0];
            var methodInfo = ((obj is null) ? type : obj.GetType()).GetMethod(finalMethod, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new InvalidOperationException($"Method '{finalMethod}' not found.");
            var att = methodInfo.GetCustomAttribute<ToJSAttribute>() ?? throw new InvalidOperationException("Method is missing the [ToJS] attribute");

            return methodInfo.Invoke(obj, args);
        }

        private static async Task<object?> InvokeNestedMethodAsync(object? obj, string methodChain, object[]? args, Type? type = null)
        {
            var methodParts = methodChain.Split('.');

            foreach (var part in methodParts.Take(methodParts.Length - 1))
            {
                var prop = ((obj is null) ? type : obj.GetType()).GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                obj = prop?.GetValue(obj)
                       ?? null;
            }
            
            var finalMethod = (methodParts.Length > 1) ? methodParts.Last() : methodParts[0];
            var methodInfo = ((obj is null) ? type : obj.GetType()).GetMethod(finalMethod, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new InvalidOperationException($"Method '{finalMethod}' not found.");
            var att = methodInfo.GetCustomAttribute<ToJSAttribute>() ?? throw new InvalidOperationException("Method is missing the [ToJS] attribute");

            if (methodInfo.ReturnType.IsGenericType)
            {
                var result = (Task)methodInfo.Invoke(obj, args);
                await result;
                return await (dynamic)result;
            }
            // For Task
            await (Task)methodInfo.Invoke(obj, args);
            return null;
        }
    }
}
