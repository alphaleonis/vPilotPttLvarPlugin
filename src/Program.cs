
using Microsoft.FlightSimulator.SimConnect;
using RossCarlson.Vatsim.Vpilot.Plugins;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class DcfVPilotPttPlugin : IPlugin
{
   private PttLvarPlugin? _mediator;
   
   public string Name => "Dcf-PTT";

   public void Initialize(IBroker broker)
   {
      broker.SessionEnded += Broker_SessionEnded;
      _mediator = new PttLvarPlugin(broker);
      _mediator.Start();      
   }

   private void Broker_SessionEnded(object sender, EventArgs e)
   {
      _mediator?.Stop();
   }
}

