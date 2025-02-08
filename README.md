# DotJS

**DotJS** is a lightweight C# library for seamless communication between the browser and the server, making it easy to call JavaScript methods from C# and vice versa. This README will guide you through the basics of setting up and using DotJS.

---

## Features

- **JavaScript Integration**: Inject JavaScript code directly into the browser using the `Script` property.
- **Async Method Invocation**: Call JavaScript methods from C# and handle the responses asynchronously.
- **Object References**: Manage JavaScript references for efficient interaction with browser-side objects.

---

## Installation

To install DotJS, add the library to your project via NuGet:

```bash
dotnet add package DotJS
```

---

## Usage

### Setting Up

To get started, initialize a new instance of the `JS` class. Here’s an example:

```csharp
using DotJS;
using Constellations;

// Initialize the Constellation instance
var constellation = new Constellation("MyServer");

// Create a JS instance
var jsInterop = new JS(
    id: "my-session-id",
    constellation: constellation,
    key: "secure-key"
);

// Access the JavaScript injection script
string script = jsInterop.Script;

// Inject the script into your HTML page
// Example: Serve it to the browser or append it to the DOM dynamically
Console.WriteLine(script);
```

### Injecting the JavaScript Script

The `Script` property contains a JavaScript snippet that establishes communication between the browser and the server. You can inject this script into your webpage via:

1. Including it as a script tag in your HTML.
2. Sending it dynamically to the client via your server framework.

### Calling JavaScript Methods from C\#

Use the `InvokeAsync` method to call JavaScript methods from C#. Here’s how:

```csharp
// Example: Call a JavaScript method named 'showAlert'
var result = await jsInterop.InvokeAsync(
    methodName: "showAlert",
    args: new object[] { "Hello from C#!" }
);

Console.WriteLine(result); // Output from the JavaScript method
```

---

## Cleanup

When you no longer need the `JS` instance, ensure proper cleanup to release resources:

```csharp
jsInterop.Dispose();
```

This will:

- Remove any user keys associated with the session.
- Unsubscribe from event handlers.

---

## Example Workflow

Here’s a complete example that demonstrates DotJS in action:

```csharp
using DotJS;
using Constellations;

class Program
{
    static async Task Main(string[] args)
    {
        var constellation = new Constellation("MyServer").AllowOrigins("yoursite.com").Run();

        var jsInterop = new JS("session-123", constellation, "secure-key");

        

        // Inject the script
        Console.WriteLine("Inject this script into your browser:");
        Console.WriteLine(jsInterop.Script);

        // Call a JavaScript function
        string result = await jsInterop.InvokeAsync("alert", new object[] { "Hello from DotJS!" });
        Console.WriteLine($"Result: {result}");

        // Cleanup
        jsInterop.Dispose();
    }
}
```

---

## Contributing

Feel free to open issues or submit pull requests to improve DotJS. Contributions are welcome!

---

##
