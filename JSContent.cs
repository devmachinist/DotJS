using System.Diagnostics;

namespace DotJS
{
    public class JSContent
    {
        public string GetScript(string name, string serverName, string ipaddress, int port, string key, string domain)
        {
            Debug.WriteLine(((ipaddress.Trim() == "127.0.0.1")? "localhost": domain));

            return $@"

class StopWatch {{
    constructor() {{
        this.startTime = 0;
        this.endTime = 0;
        this.running = false;
    }}

    start() {{
        if (this.running) return;
        this.startTime = performance.now();
        this.running = true;
    }}

    stop() {{
        if (!this.running) return;
        this.endTime = performance.now();
        this.running = false;
    }}

    reset() {{
        this.startTime = 0;
        this.endTime = 0;
        this.running = false;
    }}

    getElapsedTime() {{
        if (this.running) {{
            return (performance.now() - this.startTime) / 1000; // in seconds
        }} else {{
            return (this.endTime - this.startTime) / 1000; // in seconds
        }}
    }}
}}
class ObjRef {{
    constructor(Obj){{
        this.obj = Obj
        this.invoke = async (method, args) => {{
            var result = await CS.invoke(method, args, this.obj)
            return result;
        }}
    }}
}}
class EventEmitter {{
    constructor() {{
        this.events = {{}};
    }}

    on(event, listener) {{
        if (!this.events[event]) {{
            this.events[event] = [];
        }}
        this.events[event].push(listener);
    }}

    off(event, listener) {{
        if (!this.events[event]) return;
        this.events[event] = this.events[event].filter(l => l !== listener);
    }}

    emit(event, ...args) {{
        if (!this.events[event]) return;
        this.events[event].forEach(listener => listener(...args));
    }}
}}

class Message {{
    constructor(sender, recipient, payload, isBroadcast = false) {{
        this.Sender = sender;
        this.Recipient = recipient;
        this.Payload = payload;
        this.Server = null;
        this.Broadcast = isBroadcast;           ;
        this.Timestamp = Date.now().toISOString();
    }}
}}

class Constellation extends EventEmitter {{
    constructor(name, url) {{
        super();
        this.name = name;
        this.server = null;
        this.url = url;
        this.socket = this._createWebSocket((window.location.href.startsWith(""https://"") ? ""wss://"" : ""ws://"")+ url+'/?id={name}');
        this._setupEventHandlers();
        this.connected = false;
        this.encryptionKey = ""{key}"";
        this.isReady = false;
        this.readyActions = [];
    }}
    onReady(action){{
        if(this.isReady === false){{
            this.readyActions.push(action);
        }}
        else{{action()}}
    }}
    Ready(){{
        this.readyActions.forEach(action => action());
        this.isReady = true;
        var event = new Event('ConstellationLoaded');
        document.dispatchEvent(event);
    }}

    _createWebSocket(url) {{
        if (typeof WebSocket !== 'undefined') {{
            return new WebSocket(url);
        }} else {{
            const WebSocket = require('ws');
            return new WebSocket(url);
        }}
    }}

    _setupEventHandlers() {{
        this.socket.onopen = () => {{
            this.connected = true;
            this.emit(""ready"");
            this.Ready();
            // Listen for possible errors
            this.socket.addEventListener(""error"", (event) => {{
              console.log(""WebSocket error: "", event);
            }});
        }};

        this.socket.onmessage = async (event) => {{
            let decoder = new TextDecoder(""utf-8"");
            var data = await this.decrypt(event.data)
            data = data.replace(""::ENDOFMESSAGE::"", """");

            if (this._isJsonString(data)) {{
                const message = JSON.parse(data);
                    this._handleMessage(message);
            }}
            else {{
                this.emit('messageUnreadable', data)
            }}
        }};

        this.socket.onclose = () => {{
            this.emit(""connectionLost"");

        }};
    }}

    async sendMessage(recipient, payload, isBroadcast = false) {{
        var encoder = new TextEncoder();
        const message = {{
            Sender: this.name,
            Recipient: recipient,
            Payload: payload,
            Server: this.server,
            Broadcast: isBroadcast,
            Timestamp: new Date().toISOString()
        }};
        var pk = await this.encrypt(JSON.stringify(message) + ""::ENDOFMESSAGE::"");
        var length = pk.length;
        try{{
            await this.socket.send(""[[[""+length+""]]]""+pk);
            this.emit(""sentMessage"", message);
        }}
        catch(ex){{ console.error(ex)}}
    }}

    disconnect(sender) {{
        const disconnectMessage = {{
            Sender: sender,
            Recipient: this.serverName,
            Payload: ""::DISCONNECT::"",
            Server: null,
            Timestamp: new Date().toISOString()
        }};
        this.socket.send(this.encrypt(JSON.stringify(disconnectMessage)+""::ENDOFMESSAGE::""));
        this.socket.close();
    }}

    _handleMessage(message) {{
        this.emit('newMessage', message);
    }}

    _isJsonString(str) {{
        try {{
            JSON.parse(str);
            return true;
        }} catch (e) {{
            return false;
        }}
    }}
    async convertTo256BitKey(input) {{
        const encoder = new TextEncoder();
        const data = encoder.encode(input);
        const hash = await crypto.subtle.digest('SHA-256', data);

        // Return the hash, which is 32 bytes long (256 bits)
        return hash;
    }}
async encrypt(plainText) {{
    try {{
        const key = await this.convertTo256BitKey(this.encryptionKey);
        const iv = new Uint8Array(16); // Initialization vector (16 bytes for AES)
        const cryptoKey = await crypto.subtle.importKey(
            'raw',
            key,
            {{ name: 'AES-CBC', length: 256 }},
            false,
            ['encrypt']
        );

        const chunkSize = 800000 * 800000; // 1MB chunk size (you can adjust this)
        const encoder = new TextEncoder();
        let encryptedChunks = [];

        // Process the string in chunks
        for (let i = 0; i < plainText.length; i += chunkSize) {{
            const chunk = plainText.slice(i, i + chunkSize);
            const encryptedChunk = await crypto.subtle.encrypt(
                {{ name: 'AES-CBC', iv: iv }},
                cryptoKey,
                encoder.encode(chunk)
            );

            // Convert the encrypted chunk (Uint8Array) to a binary string
            let binaryString = '';
            const bytes = new Uint8Array(encryptedChunk);
            for (let j = 0; j < bytes.byteLength; j++) {{
                binaryString += String.fromCharCode(bytes[j]);
            }}

            // Push Base64 encoded chunk
            encryptedChunks.push(btoa(binaryString));
        }}

        // Combine all chunks into one Base64 string
        const base64String = encryptedChunks.join('');

        return base64String;
    }} catch (err) {{
        console.error(err.message);
        return '';
    }}
}}
        async decrypt(cipherText) {{
        try {{
            const key = await this.convertTo256BitKey(this.encryptionKey);
            const iv = new Uint8Array(16); // Same IV used during encryption

            const cryptoKey = await crypto.subtle.importKey(
                'raw',
                key,
                {{ name: 'AES-CBC', length: 256 }},
                false,
                ['decrypt']
            );

            // Convert Base64 to Uint8Array
            const encryptedData = Uint8Array.from(atob(cipherText), c => c.charCodeAt(0));
            const decrypted = await crypto.subtle.decrypt(
                {{ name: 'AES-CBC', iv: iv }},
                cryptoKey,
                encryptedData
            );

            const decoder = new TextDecoder();
            console.log(decoder.decode(decrypted));
            return decoder.decode(decrypted);
        }} catch (err) {{
            console.error(err.message);
            return '';
        }}
    }}
}}
function invoker(invocation) {{
    const jsReturn = {{
        Instance: invocation.Instance,
        Value: ''
    }};

    try {{
        let target = invocation.Ref || globalThis;
        const methodParts = invocation.Method.split('.');

        // Traverse through the nested properties/methods
        for (let i = 0; i < methodParts.length - 1; i++) {{
            target = target[methodParts[i]];
            if (target === undefined || target === null) {{
                throw new Error(`Property '${{methodParts[i]}}' is undefined or null.`);
            }}
        }}

        const finalMethod = target[methodParts[methodParts.length - 1]];
        if (typeof finalMethod !== 'function') {{
            throw new Error(`Method '${{methodParts[methodParts.length - 1]}}' not found.`);
        }}

        const result = finalMethod.apply(invocation.Ref ? target : null, invocation.Args);
        jsReturn.Value = result !== undefined ? result : '';
    }} catch (error) {{
        jsReturn.Value = `Error: ${{error.message}}`;
    }}

    return jsReturn;
}}
function newGuid() {{
    // Generate a Guid v4
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {{
        var r = Math.random() * 16 | 0,
            v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    }});
}}

(() => {{
        if (""object"" == typeof globalThis)
            return globalThis;
        try {{
            return this || new Function(""return this"")()
        }} catch (e) {{
            if (""object"" == typeof window)
                return window
        }}
    }})().CS = {{
        client: new Constellation('{name}','{((ipaddress.Trim() == "127.0.0.1")? "localhost": domain)}:{port}'),
        onReady: function(action){{ CS.client.onReady(action)}}
    }}
    CS.invoke = async function(methodName, args = null, Obj = null){{
        var timer = new StopWatch();
        timer.start();
        const Instance = newGuid();
        let value = null;
        // Wrap the return handling in a Promise to properly await the result
        const result = new Promise((resolve) => {{
            const handleReturn = (message) => {{
                const rtValue = JSON.parse(message.Payload);
                if (rtValue.Instance === Instance) {{
                    CS.client.off(""newMessage"", handleReturn);
                    resolve(rtValue.Value); // Resolve the promise
                }}
            }};
            CS.client.on(""newMessage"", handleReturn);
        }});
        // Send the message after setting up the listener
        await CS.client.sendMessage(""{serverName}"", JSON.stringify({{ Instance: Instance, Method: methodName, Args: args, Ref:Obj}}));
        // Await the result from the promise
        value = await result;
        timer.stop();
        console.log(methodName+' completed in '+timer.getElapsedTime()+'seconds');
        return value;
    }};
    CS.invokeAsync = async function(methodName, args = null, Obj = null){{
        var timer = new StopWatch();
        timer.start();
        let Instance = newGuid();
        let value = null;
        // Wrap the return handling in a Promise to properly await the result
        var result = new Promise((resolve) => {{
            const handleReturn = (message) => {{
                const rtValue = JSON.parse(message.Payload);
                if (rtValue.Instance === Instance) {{
                    CS.client.off(""newMessage"", handleReturn);
                    resolve(rtValue.Value); // Resolve the promise
                }}
            }};
            CS.client.on(""newMessage"", handleReturn);
        }});
        // Send the message after setting up the listener
        var inv =  JSON.stringify({{Async:true, Instance: Instance, Method: methodName, Args: args, Ref:Obj}})
        console.log(inv);
        await CS.client.sendMessage(""{serverName}"", inv);
        // Await the result from the promise
        value = await result;
        console.log(value);
        timer.stop();
        console.log(methodName+' completed in '+timer.getElapsedTime()+'seconds');
        return value;
    }};

    CS.JSRefs = [];
    CS.createCSRef = (Obj) => {{
        return new ObjRef(Obj);
    }}
    CS.addJSRef = (Obj) => {{
        JSRefs.push(Obj);
        return Obj;
    }}
    const handleInvocation = (message) => {{
            (async () => {{
                var invocation = JSON.parse(message.Payload);
                if(invocation.Instance){{
                    if(invocation.Method){{
                        var result = await invoker(invocation)
                        await CS.client.sendMessage(""{serverName}"", JSON.stringify(result));
                    }}
                }}
            }})()
    }};
    CS.client.on(""newMessage"", handleInvocation)
";
        }
    }
}
