
using Microsoft.FlightSimulator.SimConnect;
using RossCarlson.Vatsim.Vpilot.Plugins;
using System;
using System.Threading;
using System.Threading.Tasks;

public class PttLvarPlugin
{
   public const string LVarName = "DCF_VPILOT_PTT";

   private SimConnect? _simConnect;
   private readonly AutoResetEvent _eventHandle = new AutoResetEvent(false);
   private CancellationTokenSource? _cancellationTokenSource;
   private Task? _task;
   private readonly IBroker _broker;

   public PttLvarPlugin(IBroker broker)
   {
      _broker = broker;
   }

   private void RecoverFromError(Exception exception)
   {
      Log($"Exception received {exception.Message}");
      CloseConnection();
   }

   public void CloseConnection()
   {
      try
      {
         _simConnect?.Dispose();
      }
      catch
      {
      }

      _simConnect = null;
   }


   public void Start()
   {
      if (Interlocked.CompareExchange(ref _cancellationTokenSource, new CancellationTokenSource(), null) == null)
      {
         Log("Starting vPilot PTT LVar Plugin");
         _cancellationTokenSource = new CancellationTokenSource();
         _task = Task.Run(() => Run(_cancellationTokenSource.Token));
      }
   }

   public void Stop()
   {
      var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
      if (cts != null)
      {
         Log("Stopping vPilot PTT LVar Plugin");
         cts.Cancel();
         _task?.GetAwaiter().GetResult();
      }
   }

   private async Task Run(CancellationToken cancellationToken = default)
   {
      try
      {
         bool wasConnected = false;
         while (!cancellationToken.IsCancellationRequested)
         {
            if (_simConnect == null)
            {
               if (wasConnected)
                  Log("Connection to simulator lost.");

               _simConnect = await Connect(cancellationToken);
               wasConnected = true;
            }
            else
            {
               try
               {
                  await _eventHandle.WaitOneAsync(cancellationToken);
                  _simConnect?.ReceiveMessage();
               }
               catch (OperationCanceledException)
               {
                  CloseConnection();
                  return;
               }
               catch (Exception ex)
               {
                  RecoverFromError(ex);
                  if (cancellationToken.IsCancellationRequested)
                  {
                     return;
                  }
               }
            }
         }
      }
      finally
      {
         _cancellationTokenSource = null;
      }
   }

   private void Log(string message)
   {
      if (_broker != null)
         _broker.PostDebugMessage(message);
      else
         Console.WriteLine(message);
   }

   private enum DataDefinitions
   {
      VPilotPttLVar
   }

   private enum DataRequests
   {
      VPilotPttLVar
   }

   public async Task<SimConnect> Connect(CancellationToken cancellationToken)
   {
      while (true)
      {
         try
         {
            Log("Attempt connection to SimConnect...");
            var connection = new SimConnect("My Client", IntPtr.Zero, 0, _eventHandle, 0);
            cancellationToken.ThrowIfCancellationRequested();
            Log("Connected to simulator.");
            connection.OnRecvQuit += Connection_OnRecvQuit;
            connection.OnRecvSimobjectData += Connection_OnRecvSimobjectData;

            connection.AddToDataDefinition(DataDefinitions.VPilotPttLVar, $"L:{LVarName}", "Bool", SIMCONNECT_DATATYPE.INT32, 0.0f, 0);
            connection.RegisterDataDefineStruct<bool>(DataDefinitions.VPilotPttLVar);
            connection.RequestDataOnSimObject(DataRequests.VPilotPttLVar, DataDefinitions.VPilotPttLVar, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
            connection.SetDataOnSimObject(DataDefinitions.VPilotPttLVar, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_DATA_SET_FLAG.DEFAULT, false);

            return connection;
         }
         catch (Exception ex)
         {
            Log($"error: {ex.Message}");
         }

         await Task.Delay(1000, cancellationToken);
      }
   }

   private void Connection_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
   {
      if (data.dwDefineID == (uint)DataDefinitions.VPilotPttLVar && data.dwData[0] is bool value)
      {
         Log($"Set PTT={value}");
         _broker.SetPtt(value);
      }
   }

   private void Connection_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
   {
      Log("Connection to SimConnect closed.");
      CloseConnection();
   }
}

