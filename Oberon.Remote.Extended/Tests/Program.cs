using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Oberon.Extended;

int assertions=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);assertions++;Console.WriteLine("PASS "+name);}
short Read(byte[] p,int i)=>BinaryPrimitives.ReadInt16LittleEndian(p.AsSpan(i));
var neutral=Protocol.NeutralPacket();
Check(neutral.Length==100&&neutral[0]==255&&neutral.Skip(1).All(v=>v==0),"neutral packet keeps only compatibility slot 1");
var p=new byte[100];
Protocol.WritePad(p,0,new(32767,0,0,-32768,32767,12345,0),new(Deadzone:0));
Check(Read(p,1)==32767&&Read(p,7)==-32767,"axis extremes and Xbox Y convention");
Check(Read(p,9)==32767&&Read(p,11)==12345,"trigger values");
Protocol.WritePad(p,0,new(1000,1000,0,0,0,0,0),new(Deadzone:.1));
Check(Read(p,1)==0&&Read(p,3)==0,"radial deadzone removes drift");
Protocol.WritePad(p,0,new(0,-20000,0,20000,0,0,0),new(Deadzone:0,InvertLeftY:true,InvertRightY:true));
Check(Read(p,3)==20000&&Read(p,7)==-20000,"Y inversion");
int[] buttonOrder=[0,1,2,3,9,10,6,4,7,8,11,12,13,14,5];
int[] expected=[128,64,32,16,8,4,2,1,64,32,16,8,4,2,1];
for(int i=0;i<buttonOrder.Length;i++)
{
    Protocol.WritePad(p,0,new(0,0,0,0,0,0,1u<<buttonOrder[i]),new());
    Check(p[i<8?13:14]==expected[i]&&p[i<8?14:13]==0,$"SDL button {buttonOrder[i]} maps to Xbox bit");
}
Protocol.WritePad(p,0,new(0,0,0,0,0,0,1u|4u),new(SwapAB:true,SwapXY:true));
Check(p[13]==(64|16),"AB and XY swap");
Protocol.WritePad(p,3,new(0,0,0,0,0,0,1),new());Check(p[75]==255&&p[88]==128,"fourth controller offset");

Check(RemoteServer.IsLocalBindAddress("127.0.0.1"), "experimental binding accepts loopback");
Check(!RemoteServer.IsLocalBindAddress("203.0.113.123"), "experimental binding rejects nonlocal IPv4");
var rapid = new PadInput(20000,0,0,0,0,0,1u|2u);
var rapidOptions = new InputOptions(Deadzone:0, TurboA:true, TurboHz:10, StickSensitivity:1.5);
Check((InputTweaks.Apply(rapid,rapidOptions,70,1000).Buttons & 1u)==0
      && (InputTweaks.Apply(rapid,rapidOptions,70,1000).Buttons & 2u)!=0,
    "opt-in A turbo releases A only during off phase");
Check((InputTweaks.Apply(rapid,rapidOptions,0,1000).Buttons & 1u)!=0,
    "opt-in turbo retains active phase");
Check(InputTweaks.Apply(rapid,rapidOptions,0,1000).LX==30000,
    "sensitivity scales sticks without overflow");
Check(InputTweaks.Apply(rapid,new InputOptions(),70,1000).Buttons==rapid.Buttons,
    "accessibility tweaks disabled by default");

// Native XInput conversion can be tested without an attached controller or Windows DLL.
var xbox = XInput.ToPad(new XState { Pad = new XGamepad
{
    Buttons = 0x1000 | 0x0020 | 0x0001,
    LX = 17000, LY = 32767, RX = -17000, RY = -32768,
    LeftTrigger = 255, RightTrigger = 128
} });
Check((xbox.Buttons & 1u) != 0 && (xbox.Buttons & (1u << 4)) != 0 &&
      (xbox.Buttons & (1u << 11)) != 0, "XInput Xbox A, View, DPad maps into SDL bit positions");
Check(xbox.LY == -32767 && xbox.RY == 32767 && xbox.LX == 17000,
      "XInput stick axes match SDL orientation and stay in range");
Check(xbox.LT == 32767 && xbox.RT > 16000, "XInput trigger bytes scale to Oberon 16-bit");
Protocol.WritePad(p, 0, xbox, new InputOptions(Deadzone: 0));
Check(p[13] == (128 | 1) && p[14] == 16 &&
      Read(p,3) < -20000, "XInput fallback produces the original 100-byte Xbox packet");

Check(VirtualPad.DeltaToAxis(1) == 1800 && VirtualPad.DeltaToAxis(-1) == -1800,
    "small mouse moves produce gradual analog deflection");
Check(VirtualPad.DeltaToAxis(100) == 32767 && VirtualPad.DeltaToAxis(-100) == -32767,
    "large mouse motion saturates without overflow");

var defaultKeyboard = VirtualKeyMapping.Default;
Check(defaultKeyboard.MoveUp == 'W' && defaultKeyboard.A == 'J' &&
      defaultKeyboard.LT == 1 && defaultKeyboard.RT == 2,
      "default keyboard and mouse mappings are retained");
var customKeyboard = defaultKeyboard.WithKey(nameof(VirtualKeyMapping.MoveUp), 'T')
    .WithKey(nameof(VirtualKeyMapping.A), 0x31);
Check(customKeyboard.MoveUp == 'T' && customKeyboard.A == 0x31 &&
      defaultKeyboard.MoveUp == 'W' && defaultKeyboard.A == 'J',
      "editing a keyboard binding creates an immutable independent layout");
Check(!VirtualKeyMapping.IsAssignable(0x77) && !VirtualKeyMapping.IsAssignable(0x5B)
      && VirtualKeyMapping.IsAssignable(0x25),
      "F8 and Windows system keys are reserved but arrow keys remain assignable");
Check(VirtualKeyMapping.Entries.All(entry => entry.Id is not null &&
      defaultKeyboard.Get(entry.Id) != 0), "every editable control has a default key");

var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
using var server=new RemoteServer();server.Publish(p);server.Start(port);
using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
using var client=new ClientWebSocket();await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"),timeout.Token);
var buffer=new byte[512];var result=await client.ReceiveAsync(buffer,timeout.Token);
Check(result.Count>1&&buffer[0]==10,"handshake compatible with Xbox");
// These malformed inputs must not crash the server or generate replies.
await client.SendAsync(Array.Empty<byte>(),WebSocketMessageType.Binary,true,timeout.Token);
await client.SendAsync(new byte[]{0xFA,1},WebSocketMessageType.Binary,true,timeout.Token);
await client.SendAsync(new byte[]{0xFA},WebSocketMessageType.Binary,true,timeout.Token);
result=await client.ReceiveAsync(buffer,timeout.Token);
Check(result.Count==100&&buffer.AsSpan(0,100).SequenceEqual(p),"legacy request and exact packet transport");
var request=new byte[17];request[0]=0xFA;request[1]=100;
await client.SendAsync(request,WebSocketMessageType.Binary,true,timeout.Token);result=await client.ReceiveAsync(buffer,timeout.Token);
Check(server.Rumble[0]==100,"17-byte request transfers rumble");
server.Paused=true;
await client.SendAsync(request,WebSocketMessageType.Binary,true,timeout.Token);result=await client.ReceiveAsync(buffer,timeout.Token);
Check(buffer.AsSpan(0,100).SequenceEqual(neutral)&&server.Rumble.All(v=>v==0),"pause neutralizes buttons and rumble");
server.Paused=false;
await client.SendAsync(request,WebSocketMessageType.Binary,true,timeout.Token);await client.ReceiveAsync(buffer,timeout.Token);
await Task.Delay(1100,timeout.Token);Check(server.Rumble.All(v=>v==0),"stale connection vibration timeout");
await client.CloseAsync(WebSocketCloseStatus.NormalClosure,"",timeout.Token);
server.Stop();server.Start(port);Check(server.Running,"server restart releases listener");
if(args.Contains("--sdl-smoke"))
{
    if(OperatingSystem.IsLinux())
        System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(InputEngine).Assembly,
            (name,assembly,path)=>name=="SDL2"?System.Runtime.InteropServices.NativeLibrary.Load("libSDL2-2.0.so.0"):IntPtr.Zero);
    using var engine=new InputEngine(server);
    engine.Start();
    await Task.Delay(400);
    Check(engine.Error==null&&engine.IsRunning,"native SDL starts without a graphics window");
    Check(engine.Views.Length==4,"native capture publishes four UI slots");
}
Console.WriteLine($"{assertions} assertions passed.");
