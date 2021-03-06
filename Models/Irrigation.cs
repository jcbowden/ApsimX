﻿// -----------------------------------------------------------------------
// <copyright file="Irrigation.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------

namespace Models
{
    using System;
    using System.Linq;
    using System.Xml.Serialization;
    using Models.Core;
    using Soils;
    /// <summary>
    /// This model controls the irrigation events, which can be triggered using the Apply() method.
    /// </summary>
    /// <remarks>
    /// The Irrigation module does not directly applies irrigation, it checks the amount and parameters and passes them on to other
    ///  models which then take appropriate actions and made the changes effective.  
    ///  
    /// The Apply method can be called from another model or manager scripts, it has one mandatory parameter and several optionals:  
    /// - amount: The amount of irrigation to apply (mm). This is mandatory and represents the amount of water resource to be used, 
    ///    the actual amount applied will depend on the value of irrigation efficiency;  
    /// - depth: The depth at which irrigation is applied (mm). This is optional, defaulting to zero (soil surface or above). It can 
    ///    be used to define sub-surface irrigation;  
    /// - startTime: The time, since midnight, to start the irrigation (minutes). This is optional, defaulting to zero. It is only used 
    ///    by models with sub-daily time step;  
    /// - duration: The duration of the irrigation event (minutes). This is optional, defaulting to 1440min (whole day). It defines the 
    ///    irrigation intensity and is only used by few models (mostly with sub-daily time step);  
    /// - efficiency: The efficiency of the irrigation system (mm/mm). This is optional, defaulting to 1.0 (100%). Used to define the 
    ///    actual amount of irrigation applied. Variations in efficiency are assumed to be losses between source/reservoir and field;  
    /// - willIntercept: Whether irrigation can be intercepted by canopy (true/false). This is optional, with default value of false. 
    ///    If true, some model can compute interception by the canopy thus reducing the amount of water reaching the soil;  
    /// - willRunoff: Whether irrigation can run off (true/false). This is optional, defaulting to false. If set to true, the soil 
    ///    water model will include irrigation in the runoff calculations (the way it is used depends on the water model);  
    /// 
    /// The data passed using the Apply method is used only during that event, default values are used if parameters are missing.  
    /// 
    /// The Irrigation model checks for the validity of the irrigation parameters: 
    /// - Negative values are replaced by the default;   
    /// - Values exceeding the upper bound cause fatal errors;  
    /// 
    /// The actual amount applied is reported as IrrigationApplied. The value set in the apply method is reported as IrrigationTotal.  
    /// 
    /// The Irrigated event is raised to advertise that an irrigation has occurred, so other models can respond accordingly.  
    /// With the event goes the data containing the actual amount applied and the validated irrigation parameters.  
    /// 
    /// Solutes in irrigation water have not been not implemented yet.
    /// </remarks>
    [Serializable]
    [ValidParent(ParentType = typeof(Zone))]
    public class Irrigation : Model, IIrrigation
    {
        /// <summary>Access the summary model.</summary>
        [Link] private ISummary summary = null;

        /// <summary>Access the soil model.</summary>
        [Link] private Soil soil = null;

        /// <summary>Gets the amount of irrigation actually applied (mm).</summary>
        [XmlIgnore]
        public double IrrigationApplied { get; private set; }

        /// <summary>Gets or sets the depth at which irrigation is applied (mm).</summary>
        [XmlIgnore]
        public double Depth { get; set; }

        /// <summary>Gets or sets the duration of the irrigation event (minutes).</summary>
        [XmlIgnore]
        public double Duration { get; set; }

        /// <summary>Gets or sets the efficiency of the irrigation system (mm/mm).</summary>
        [XmlIgnore]
        public double Efficiency { get; set; }

        /// <summary>Gets or sets the flag for whether the irrigation can run off (true/false).</summary>
        [XmlIgnore]
        public bool WillRunoff { get; set; }

        /// <summary>Occurs when [irrigated].</summary>
        /// <remarks>
        /// Advertises an irrigation and passes its parameters, thus allowing other models to respond accordingly.
        /// </remarks>
        public event EventHandler<IrrigationApplicationType> Irrigated;

        /// <summary>Apply some irrigation.</summary>
        /// <param name="amount">The amount to apply (mm).</param>
        /// <param name="depth">The depth of application (mm).</param>
        /// <param name="duration">The duration of the irrigation event (minutes).</param>
        /// <param name="efficiency">The irrigation efficiency (mm/mm).</param>
        /// <param name="willRunoff">Whether irrigation can run off (<c>true</c>/<c>false</c>).</param>
        /// <param name="no3">Amount of NO3 in irrigation water</param>
        /// <param name="nh4">Amount of NH4 in irrigation water</param>
        public void Apply(double amount, double depth = 0.0, double duration = 1440.0, double efficiency = 1.0, bool willRunoff = false,
                          double no3 = -1.0, double nh4 = -1.0)
        {
            if (Irrigated != null && amount > 0.0)
            {
                if (depth > soil.Thickness.Sum())
                    throw new ApsimXException(this, "Check the depth for irrigation, it cannot be deeper than the soil depth");
                Depth = depth;
 
                if (duration > 1440.0)
                    throw new ApsimXException(this, "Check the duration for the irrigation, it must be less than 1440 minutes");
                Duration = duration;

                if (efficiency > 1.0)
                   throw new ApsimXException(this, "Check the value of irrigation efficiency, it must be between 0 and 1");
                Efficiency = efficiency;

                if (Depth > 0.0)
                { // Sub-surface irrigation: it cannot be intercepted nor run off directly
                    willRunoff = false;
                }

                IrrigationApplied = amount;
                WillRunoff = willRunoff;

                // Prepare the irrigation data
                IrrigationApplicationType irrigData = new IrrigationApplicationType();
                irrigData.Amount = IrrigationApplied;
                irrigData.Depth = Depth;
                irrigData.Duration = Duration;
                irrigData.WillRunoff = WillRunoff;
                if (no3 != -1)
                    irrigData.NO3 = no3;
                if (nh4 != -1)
                    irrigData.NH4 = nh4;

                // Raise event and write log
                Irrigated.Invoke(this, irrigData);
                summary.WriteMessage(this, string.Format("{0:F1} mm of water added via irrigation at depth {1} mm", IrrigationApplied, Depth));
            }
            else
            {
                // write log of aborted event
                summary.WriteMessage(this, "Irrigation did not occur because the amount given was negative");
            }
        }

        /// <summary>Called when [do daily initialisation].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoDailyInitialisation")]
        private void OnDoDailyInitialisation(object sender, EventArgs e)
        {
            IrrigationApplied = 0.0;
            Depth = 0.0;
            Duration = 1440.0;
            WillRunoff = false;
        }
    }
}
