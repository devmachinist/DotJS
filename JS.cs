using Constellations;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace DotJS
{
    public class JS 
    {
        private readonly Constellation _constellation;
        public string Script { get; set; }
        public string Id { get; set; }
        public string Key { get; set; }
        public List<object> CSRefs { get; set; }
        public List<JSRef> JSRefs { get; set; }

        public JS(string id, Constellation constellation, string key)
        {
            Id = id;
            Key = key;
            _constellation = constellation;
            _constellation.SetUserKey(id, key);
            _constellation.NewMessage += invocationHandler;

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                              Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                              "Development";
            Script = new JSContent().GetScript(Id, 
                                               constellation.Name,
                                               constellation.Listener.Address?? constellation.Listener.Name,
                                               constellation.Listener.Port,
                                               key,
                                               (environment != "Production")?"localhost": Dns.GetHostName());
        }

        private async void invocationHandler(Constellation.Message message)
        {
            var timer = new Stopwatch();
            await Task.Run(async () =>
            {

                var (isinv,inv) = IsInvocation(message.Payload);
                if (isinv)
                {
                    if (message.Sender == Id)
                    {
                        JSReturn? jsreturn = null;
                        if(inv.Async == true)
                        {
                            jsreturn = await Invoker.RunAsync(inv);
                        }
                        else
                        {
                            jsreturn = Invoker.Run(inv);
                        }

                        _ = _constellation.SendMessage(message.Sender, JsonSerializer.Serialize(jsreturn), null, null, false, Id);
                    }
                }
            });
            var running = timer.IsRunning;
        }

        private (bool, Invocation?) IsInvocation(string inv)
        {
            Invocation? invocation;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(inv))
                {
                    JsonElement root = doc.RootElement;

                    // Extract the basic fields
                    bool async = root.GetProperty("Async").GetBoolean();
                    string instance = root.GetProperty("Instance").GetString();
                    string method = root.GetProperty("Method").GetString();
                    string? refElement = root.GetProperty("Ref").GetString();

                    // Check if Ref is null or has a value
                    // Extract and handle Args (which is a double-stringified array)
                    object?[]? argsString = root.GetProperty("Args").Deserialize<object?[]?>();

                    object?[]? parsedArgs;
                    object? parsedRef;

                    // Try to parse the double-stringified Args array
                    try
                    {
                        if (argsString == null)
                        {
                            parsedArgs = null;
                        }
                        else
                        {
                            parsedArgs = argsString;
                            int i = 0;
                            foreach(var arg in parsedArgs)
                            {
                                parsedArgs[i] = ((JsonElement)parsedArgs[i]).ToString();
                                i++;
                            }
                        }
                        if (refElement == null)
                        {
                            parsedRef = null;
                        }
                        else
                        {
                            parsedRef = JsonSerializer.Deserialize<object?>(refElement);
                        }
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidOperationException($"Failed to parse Args: {ex.ToString()}");
                    }

                    // Now construct the Invocation object with the manually parsed values
                    invocation = new Invocation
                    {
                        Async = async,
                        Instance = instance,
                        Method = method,
                        Args = parsedArgs,
                        Ref = parsedRef
                    };
                }
                return (true, invocation);
            }
            catch(Exception ex)
            {
                return (false, null);
            }
        }
        public object AddCSRef(object obj)
        {
            CSRefs.Add(obj);
            return obj;
        }
        public JSRef CreateJSRef(object obj)
        {
            return new JSRef(this, obj);
        }

        public async Task<string> InvokeAsync(string methodName, object[]? args = null, object? objref = null)
        {
            var instance = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();

            // Define the event handler
            void handle(Constellation.Message message)
            {
                if (IsJSReturn(message.Payload))
                {
                    var jsreturn = JsonSerializer.Deserialize<JSReturn>(message.Payload);
                    if (jsreturn.Instance == instance)
                    {
                        _constellation.NewMessage -= handle; // Unsubscribe from the event
                        tcs.SetResult(jsreturn.Value); // Set the result to complete the task
                    }
                }
            }

            _constellation.NewMessage += handle; // Subscribe to the event

            // Send the message to the constellation
            await _constellation.SendMessage(Id, JsonSerializer.Serialize(new Invocation
            {
                Async = false,
                Ref = objref,
                Method = methodName,
                Args = args,
                Instance = instance
            }), null, null, false, Id);

            // Await the task completion source
            return await tcs.Task;
        }
        private bool IsJSReturn(string data)
        {
            try
            {
                JsonSerializer.Deserialize<JSReturn>(data);
                return true;
            }
            catch (Exception ex) {
                return false;
            }
        }

        public void Dispose()
        {
            _constellation.RemoveUserKey(Id);
            _constellation.NewMessage -= invocationHandler;
        }

        public class JSReturn
        {
            public string Instance { get; set; }
            public string Value { get; set; }
            public JSReturn(){ }
        }
        public class Invocation
        {
            public bool? Async { get; set; }
            public object? Ref { get; set; }
            public string Instance { get; set; }
            public string Method { get; set; }
            public object?[]? Args { get; set; }
        }
        public class JSRef
        {
            public object Ref { get; set; }
            public JS js { get; set; }
            public JSRef(JS jS, object reF){
                js = jS;
                Ref = reF;
            }
            public async Task<string> Invoke(string method, object[]? args = null)
            {
                return await js.InvokeAsync(method, args, Ref);
            }          
        }
    }
}
