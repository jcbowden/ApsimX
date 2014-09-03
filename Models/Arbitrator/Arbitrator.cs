﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Models.Core;
using System.Xml.Serialization;
using Models.PMF;
using Models.Soils;

namespace Models.Arbitrator
{

    /// <summary>
    /// Vals u-beaut soil arbitrator
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class Arbitrator : Model
    {

        [Link]
        Soils.Soil Soil = null;

        [Link]
        WeatherFile Weather = null;

        [Link]
        Summary Summary = null;

        ICrop2[] plants;

        /// <summary>
        /// This will hold a range of arbitration methods for testing - will eventually settle on one standard method
        /// </summary>
        [Description("Arbitration method: old / new - use old - new only for testing")]
        public string ArbitrationMethod { get; set; }
        [Description("Potential nutrient uptake method: 1=where the roots are; 2=PMF concentration based; 3=OilPlan amount based")]
        public int NutrientUptakeMethod { get; set; }

        /// <summary>
        /// Potential nutrient supply in layers (kgN/ha/layer) - this is the potential (demand-limited) supply if this was the only plant in the simualtion
        /// </summary>
        [XmlIgnore]
        [Description("Potential nutrient supply in layers for the first plant")]
        public double[] Plant1potentialSupplyNitrogenPlantLayer
        {
            get
            {
                double[] result = new double[Soil.SoilWater.dlayer.Length];
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    result[j] = potentialSupplyNitrogenPlantLayer[0, j];
                }
                return result;
            }
        }


        /// <summary>
        /// Potential nutrient supply in layers (kgN/ha/layer) - this is the potential (demand-limited) supply if this was the only plant in the simualtion
        /// </summary>
        [XmlIgnore]
        [Description("Potential nutrient supply in layers for the second plant")]
        public double[] Plant2potentialSupplyNitrogenPlantLayer
        {
            get
            {
                double[] result = new double[Soil.SoilWater.dlayer.Length];
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    result[j] = potentialSupplyNitrogenPlantLayer[1, j];
                }
                return result;
            }
        }


        // Plant variables
        double[] demandWater;
        double[,] potentialSupplyWaterPlantLayer;
        double[,] uptakeWaterPlantLayer;

        double[] demandNitrogen;
        double[,] potentialSupplyNitrogenPlantLayer;
        double[,] potentialSupplyPropNO3PlantLayer;
        double[,] uptakeNitrogenPlantLayer;
        double[,] uptakeNitrogenPropNO3PlantLayer;

        // soil water evaporation stuff
        //public double ArbitEOS { get; set; }  //

        /// <summary>
        /// The NitrogenChangedDelegate is for the Arbitrator to set the change in nitrate and ammonium in SoilNitrogen
        /// </summary>
        /// <param name="Data"></param>
        public delegate void NitrogenChangedDelegate(Soils.NitrogenChangedType Data);
        /// <summary>
        /// To publish the change event
        /// </summary>
        public event NitrogenChangedDelegate NitrogenChanged;

        class CanopyProps
        {
            /// <summary>
            /// Grean leaf area index (m2/m2)
            /// </summary>
            public double laiGreen;
            /// <summary>
            /// Total leaf area index (m2/m2)
            /// </summary>
            public double laiTotal;
        }
        //public CanopyProps[,] myCanopy;

        // new Arbitration parameters from here
        int zones;
        int bounds;
        int forms;
        double[] demandByPlant;
        double[, , ,] lowerBound;
        double[, ,] rootExploration;
        double[, ,] uptakePreference;
        double[, , , ,] uptakeParameterOnAmountInLayer;
        double[, , , ,] uptakeParameterOnConcentrationInSoil;
        double[, , , ,] resource;
        double[, , , ,] extractable;
        double[, , , ,] demand;
        double[, , , ,] demandForResource;
        double[, , , ,] uptake;
        double[] extractableByPlant;

        string resourceToArbitrate;
        int[] plantIndexLowerBound;

        /// <summary>
        /// Runs at the start of the simulation, here only reads the aribtration method to be used
        /// </summary>
        public override void OnSimulationCommencing()
        {
            // Check that ArbitrationMethod is valid
            if (ArbitrationMethod.ToLower() == "old" || ArbitrationMethod.ToLower() == "new") ;
            // nothing, all good
            else
                throw new Exception("Invalid AribtrationMethod selected");

            // get the list of crops in the simulation
            plants = this.Plants;

            // myCanopy = new CanopyProps[plants.Length, 0];  // later make this the layering in the canopy
            // for (int i = 0; i < plants.Length; i++)
            // {
            //    myCanopy[0, 0].laiGreen = plants[i].CanopyProperties.LAI;
            // }

            // size the arrays
            demandWater = new double[plants.Length];
            potentialSupplyWaterPlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];
            uptakeWaterPlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];

            demandNitrogen = new double[plants.Length];
            potentialSupplyNitrogenPlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];
            potentialSupplyPropNO3PlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];
            uptakeNitrogenPlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];
            uptakeNitrogenPropNO3PlantLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length];

            // new Arbitration parameters from here
            zones = 1; // as a starting point - will we need to consider redimensioning?  Depends on when the root system information is available - or just allow for all zones beneath current location
            bounds = 1;  // this will need more attention - needs ot be derived from the plant lower limit information and can be differnet for water and   
            forms = 2;  // 
            demandByPlant = new double[plants.Length];
            lowerBound = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds];
            rootExploration = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones];
            uptakePreference = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones];
            uptakeParameterOnAmountInLayer = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            uptakeParameterOnConcentrationInSoil = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            resource = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            extractable = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            demand = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            demandForResource = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            uptake = new double[plants.Length, Soil.SoilWater.dlayer.Length, zones, bounds, forms];
            extractableByPlant = new double[plants.Length];
            plantIndexLowerBound = new int[plants.Length];


        }


        [EventSubscribe("DoDailyInitialisation")]
        private void OnDailyInitialisation(object sender, EventArgs e)
        {
            // many of these arrays are being used for water and nitrogen so shoudl move at last some of the zeroing to the OnDoXxxArbitration routines
            Utility.Math.Zero(demandWater);
            Utility.Math.Zero(potentialSupplyWaterPlantLayer);
            Utility.Math.Zero(uptakeWaterPlantLayer);

            Utility.Math.Zero(demandNitrogen);
            Utility.Math.Zero(potentialSupplyNitrogenPlantLayer);
            Utility.Math.Zero(uptakeNitrogenPlantLayer);
            Utility.Math.Zero(uptakeNitrogenPropNO3PlantLayer);

            // new Arbitration parameters from here
            Utility.Math.Zero(demandByPlant);
            Utility.Math.Zero(extractableByPlant);

            for (int p = 0; p < plants.Length; p++)
            {
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++)
                    {
                        rootExploration[p, l, z] = 0.0;
                        uptakePreference[p, l, z] = 0.0;
                        for (int b = 0; b < bounds; b++)
                        {
                            lowerBound[p, l, z, b] = 0.0;
                            for (int f = 0; f < forms; f++)
                            {
                                uptakeParameterOnAmountInLayer[p, l, z, b, f] = 0.0;
                                uptakeParameterOnConcentrationInSoil[p, l, z, b, f] = 0.0;
                                resource[p, l, z, b, f] = 0.0;
                                extractable[p, l, z, b, f] = 0.0;
                                demand[p, l, z, b, f] = 0.0;
                                uptake[p, l, z, b, f] = 0.0;
                                demandForResource[p, l, z, b, f] = 0.0;
                            }
                        }

                    }
                }
            }
        }

        [EventSubscribe("DoWaterArbitration")]
        private void OnDoWaterArbitration(object sender, EventArgs e)
        {
            if (ArbitrationMethod.ToLower() == "new")
            {
                DoArbitration("water");
            }
            else
            {
                Old_OnDoWaterArbitration();
            }
        }

        [EventSubscribe("DoNutrientArbitration")]
        private void OnDoNutrientArbitration(object sender, EventArgs e)
        {
            if (ArbitrationMethod.ToLower() == "new")
            {
                //Old_OnDoNutrientArbitration();
                DoArbitration("nitrogen");  
            }
            else
            {
                Old_OnDoNutrientArbitration();
            }
        }
        
        private void DoArbitration(string resourceToArbitrate)
        {
         //for (int p = 0; p < plants.Length; p++)
         //for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
         //for (int z = 0; z < zones; z++)
         //for (int b = 0; b < bounds; b++)
         //for (int f = 0; f < forms; f++)

            // when finished here would look across all the bounds in the plants and calculate how many bound levels there are
            // then would use this information with the forms to assign values into the 5-D resource array


            // set up the forms of the resource that can satisfy the demand
            if (resourceToArbitrate.ToLower() == "water")
            {
                forms = 1;
            }
            else if (resourceToArbitrate.ToLower() == "nitrogen")
            {
                forms = 2;  // ie NO3 and NH4
            }
            else
            {
                throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
            }
            // ?? should redimension and zero the arrays at this point I think - need to find out the number of bounds first if are redimensioning

            #region // bounds rubbish

            // set up the bounds here  // have given up on this for now - too hard to get my head around
            double tolerance = 0.01;
            if (resourceToArbitrate.ToLower() == "water")
            {
                // see if all the lower limits are within "tolerance" of each other, if they are then bounds = 1 and use the lower of all the lower limits
                // else sort the crops by lower limit and start calculating the number and values in the bounds
                // for now just assume two crops and take the lower

                /*
                // got myself into a total tangle here comment out for now, go back to a single bound of the minimum lower limit

                if (plants.Length == 1)
                {
                    bounds = 1;
                    plantIndexLowerBound[0] = 0;
                }
                else
                {
                    // should probably do this on the sum of the lower limits but this will do for now
                    if ((plants[0].RootProperties.LowerLimitDep[0] - plants[1].RootProperties.LowerLimitDep[0]) > (tolerance * (plants[0].RootProperties.LowerLimitDep[0] - plants[1].RootProperties.LowerLimitDep[0]) / 2.0))
                    {
                        // the difference in the  lower bounds is greater than the tolerance 
                        bounds = 2;
                        if (plants[0].RootProperties.LowerLimitDep[0] < plants[1].RootProperties.LowerLimitDep[0])  // plant[0] can extract the most water
                        {
                            plantIndexLowerBound[0] = 0;
                            plantIndexLowerBound[1] = 1;
                        }
                        else
                        {
                            plantIndexLowerBound[0] = 1;
                            plantIndexLowerBound[1] = 0;
                        }
                    }
                    else
                    {
                        bounds = 1;
                        plantIndexLowerBound[0] = 0;
                    }
                }

                // this is the end of the part where I was trying to sort out the bounds - for now just set to 1
                */



                bounds = 1;  // assume they are all have the same lower bound until I figure this out
                for (int p = 0; p < plants.Length; p++)  // plant is not relevant for resource so omit that loop, p always = 0
                {
                    for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                    {
                        for (int z = 0; z < zones; z++) // for now set zones is to 1
                        {
                            for (int b = 0; b < bounds; b++)
                            {
                                if (p == 0)
                                {
                                    lowerBound[0,l,z,b] = plants[p].RootProperties.LowerLimitDep[l];  // the index of 0 rather than p is intentional as the lower bounds apply to all plants
                                }
                                else
                                {
                                    if (plants[p].RootProperties.LowerLimitDep[l] < lowerBound[0,l,z,b])
                                    {
                                        lowerBound[0,l,z,b] = plants[p].RootProperties.LowerLimitDep[l];
                                    }
                                }
                            }
                        }
                    }
                }

            }
            else if (resourceToArbitrate.ToLower() == "nitrogen")
            {
                bounds = 1;  // assume they are all have the same lower bound
            }
            else
            {
                throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
            }
            // finished setting the bounds 

            #endregion

            #region // resource calculation

            // calculate resource 
            //double tempLowerBoundForResource = Math.Min(plants[0].RootProperties.LowerLimitDep[l], plants[1].RootProperties.LowerLimitDep[l]); 

            
            for (int p = 0; p < 1; p++)  // plant is not relevant for resource so omit that loop, p always = 0
            {
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++) // for now set zones is to 1
                    {
                        for (int b = 0; b < bounds; b++)  
                        {
                            for (int f = 0; f < forms; f++)  
                            {
                                if (resourceToArbitrate.ToLower() == "water")
                                {
                                    //if (bounds == 1) 
                                    //{
                                        //double tempLowerBoundForResource = plants[plantIndexLowerBound[b]].RootProperties.LowerLimitDep[l]; 
                                        //resource[p, l, z, b, f] = Math.Max(0.0, Soil.SoilWater.sw_dep[l] - tempLowerBoundForResource);
                                    
                                        resource[p, l, z, b, f] = Math.Max(0.0, (Soil.SoilWater.sw_dep[l] - lowerBound[0,l,z,b]));
                                    //}
                                    //else
                                    //{
                                        // this is OK for now because are only allowing two bounds (and two crops :-) )
                                    //    resource[p, l, z, b, f] = Math.Max(0.0, Soil.SoilWater.sw_dep[l] - tempLowerBoundForResource);  // I dunno
                                    //}
                                }
                                else if (resourceToArbitrate.ToLower() == "nitrogen")
                                {
                                    if (f == 0)
                                    {
                                        resource[p, l, z, b, f] = Math.Max(0.0, Soil.SoilNitrogen.no3[l]);
                                    }
                                    else
                                    {
                                        resource[p, l, z, b, f] = Math.Max(0.0, Soil.SoilNitrogen.nh4[l]);
                                    }
                                }
                                else 
                                {
                                    throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region // extractable calculation

            // calculate extractable - for now bounds and forms = 1 - not that because this is across mulitple plants, extractable can > resource
            for (int p = 0; p < plants.Length; p++)  
            {
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++) // for now set zones is to 1
                    {
                        for (int b = 0; b < bounds; b++)  // for now set bounds is to 1
                        {
                            for (int f = 0; f < forms; f++)  // for now set forms is to 1
                            {
                                if (resourceToArbitrate.ToLower() == "water")
                                {
                                    extractable[p, l, z, b, f] = plants[p].RootProperties.UptakePreferenceByLayer[l]   // later add in zone
                                                               * plants[p].RootProperties.RootExplorationByLayer[l]    // later add in zone
                                                               * plants[p].RootProperties.KL[l]                        // later add in zone
                                                               * resource[0, l, z, b, f];                              // the usage of 0 instead of p is intended - there is no actual p dimension in resource
                                                               //* Math.Max(0.0, (Soil.SoilWater.sw_dep[l] - plants[p].RootProperties.LowerLimitDep[l]));   // later add in zone for soil water dep and bounds and figure our how to do form
                                    extractableByPlant[p] += extractable[p, l, z, b, f];  
                                }
                                else if (resourceToArbitrate.ToLower() == "nitrogen")
                                {
                                    double relativeSoilWaterContent = Utility.Math.Constrain(Utility.Math.Divide((Soil.SoilWater.sw_dep[l] - Soil.SoilWater.ll15_dep[l]), (Soil.SoilWater.dul_dep[l] - Soil.SoilWater.ll15_dep[l]), 0.0), 0.0, 1.0);
                                    if (f == 0)
                                    {
                                        extractable[p, l, z, b, f] = relativeSoilWaterContent
                                                                   * plants[p].RootProperties.UptakePreferenceByLayer[l]   // later add in zone
                                                                   * plants[p].RootProperties.RootExplorationByLayer[l]    // later add in zone
                                                                   * plants[p].RootProperties.KNO3                        // later add in zone
                                                                   * Soil.SoilNitrogen.no3ppm[l]
                                                                   * resource[0, l, z, b, f];   // the usage of 0 instead of p is intended - there is no actual p dimension in resource
                                    }
                                    else
                                    {
                                        extractable[p, l, z, b, f] = relativeSoilWaterContent
                                                                   * plants[p].RootProperties.UptakePreferenceByLayer[l]   // later add in zone
                                                                   * plants[p].RootProperties.RootExplorationByLayer[l]    // later add in zone
                                                                   * plants[p].RootProperties.KNH4                        // later add in zone
                                                                   * Soil.SoilNitrogen.nh4ppm[l]
                                                                   * resource[0, l, z, b, f];   // the usage of 0 instead of p is intended - there is no actual p dimension in resource
                                    }
                                    extractableByPlant[p] += extractable[p, l, z, b, f];
                                }
                                else
                                {
                                    throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
                                }
                            }
                        }
                    }
                }
            }

            #endregion 

            #region // demand distribution calculation

            // calculate demand distributed over layers etc - for now bounds and forms = 1
            for (int p = 0; p < plants.Length; p++)  // plant is not relevant for resource
            {
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++) // for now set zones is to 1
                    {
                        for (int b = 0; b < bounds; b++)  // for now set bounds is to 1
                        {
                            for (int f = 0; f < forms; f++)  // for now set forms is to 1
                            {
                                // demand here is a bad name as is limited by extractable - satisfyable demand if solo plant
                                if (resourceToArbitrate.ToLower() == "water")
                                {
                                    demand[p, l, z, b, f] = Math.Min(plants[p].demandWater, extractableByPlant[p])                                                // ramp back the demand if not enough extractable resource
                                                          * Utility.Math.Constrain(Utility.Math.Divide(extractable[p, l, z, b, f], extractableByPlant[p], 0.0), 0.0, 1.0);   // anmd then distribute it pver layers etc - realitically do not need the Constrain in here
                                    demandForResource[0, l, z, b, f] += demand[p, l, z, b, f]; // this is the summed demand of all the plants for the layer, zone, bound and form - retain the first dimension for convienience
                                }
                                else if (resourceToArbitrate.ToLower() == "nitrogen")
                                {
                                    demand[p, l, z, b, f] = Math.Min(plants[p].demandNitrogen, extractableByPlant[p])                                                // ramp back the demand if not enough extractable resource
                                                          * Utility.Math.Constrain(Utility.Math.Divide(extractable[p, l, z, b, f], extractableByPlant[p], 0.0), 0.0, 1.0);   // anmd then distribute it pver layers etc - realitically do not need the Constrain in here
                                    demandForResource[0, l, z, b, f] += demand[p, l, z, b, f]; // this is the summed demand of all the plants for the layer, zone, bound and form - retain the first dimension for convienience
                                }
                                else
                                {
                                    throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region // uptake calculation

            // calculate uptake distributed over layers etc - for now bounds and forms = 1
            for (int p = 0; p < plants.Length; p++)  // plant is not relevant for resource
            {
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++) // for now set zones is to 1
                    {
                        for (int b = 0; b < bounds; b++)  // for now set bounds is to 1
                        {
                            for (int f = 0; f < forms; f++)  // for now set forms is to 1
                            {
                                // ramp everything back if the resource < demand - note that resource is already only that which can potententially be extracted (i.e. not necessarily the total amount in the soil
                                uptake[p, l, z, b, f] = Utility.Math.Constrain(Utility.Math.Divide(resource[0, l, z, b, f], demandForResource[0, l, z, b, f], 0.0), 0.0, 1.0)   
                                                      * demand[p, l, z, b, f];

                            }
                        }
                    }
                }
            }

            #endregion

            #region // setting of the uptake values

            double[] dltSWdep = new double[Soil.SoilWater.dlayer.Length];   // to hold the changes in soil water depth
            NitrogenChangedType NUptakeType = new NitrogenChangedType();
            NUptakeType.Sender = Name;
            NUptakeType.SenderType = "Plant";
            NUptakeType.DeltaNO3 = new double[Soil.SoilWater.dlayer.Length];
            NUptakeType.DeltaNH4 = new double[Soil.SoilWater.dlayer.Length];

            for (int p = 0; p < plants.Length; p++)  
            {
                double[] dummyArray1 = new double[Soil.SoilWater.dlayer.Length];  // have to create a new array for each plant to avoid the .NET pointer thing - will have to re-think this with when zones come in
                double[] dummyArray2 = new double[Soil.SoilWater.dlayer.Length];  // have to create a new array for each plant to avoid the .NET pointer thing - will have to re-think this with when zones come in
                for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                {
                    for (int z = 0; z < zones; z++) // for now set zones is to 1
                    {
                        for (int b = 0; b < bounds; b++)  
                        {
                            for (int f = 0; f < forms; f++)  // for now set forms is to 1
                            {
                                if (resourceToArbitrate.ToLower() == "water")
                                {
                                    dummyArray1[l] += uptake[p, l, z, b, f];       // only 1 form but need to sum across bounds (when they get sorted that is!)
                                    dltSWdep[l] += -1.0 * uptake[p, l, z, b, f];   // -ve to reduce water content in the soil
                                }
                                else if (resourceToArbitrate.ToLower() == "nitrogen")
                                {
                                    dummyArray1[l] += uptake[p, l, z, b, f];       // add the forms together to give the total nitrogen uptake
                                    if (f == 0)
                                    {
                                        dummyArray2[l] += uptake[p, l, z, b, f];   // nitrate only uptake so can do the proportion before sending to the plant
                                    }
                                }
                                else
                                {
                                    throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
                                }
                            }
                        }
                    }
                }
                // set uptakes in each plant
                if (resourceToArbitrate.ToLower() == "water")
                {
                    plants[p].uptakeWater = dummyArray1;
                }
                else if (resourceToArbitrate.ToLower() == "nitrogen")
                {
                    plants[p].uptakeNitrogen = dummyArray1;
                    for (int l = 0; l < Soil.SoilWater.dlayer.Length; l++)
                    {
                        dummyArray2[l] = Utility.Math.Divide(dummyArray2[l], dummyArray1[l], 0.0);
                    }
                    plants[p].uptakeNitrogenPropNO3 = dummyArray2;
                }
                else
                {
                    throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
                }
            }
            // and finally set the changed soil resources
            if (resourceToArbitrate.ToLower() == "water")
            {
                Soil.SoilWater.dlt_sw_dep = dltSWdep;   // don't forget to look at this when zones come into play
            }
            else if (resourceToArbitrate.ToLower() == "nitrogen")
            {
                if (NitrogenChanged != null)
                    NitrogenChanged.Invoke(NUptakeType);
            }
            else
            {
                throw new Exception("Arbitrator cannot arbitrate " + resourceToArbitrate);
            }

            #endregion

        }  // end of event

        [EventSubscribe("DoEnergyArbitration")]
        private void OnDoEnergyArbitration(object sender, EventArgs e)
        {
            // i is for plants
            // j is for layers in the canopy - layers are from the top downwards

            //Agenda
            //?when does rainfall and irrigation interception happen? - deal with this later!
            // break the canopy into layers
            // calculate the light profile down the canopy and to the soil surface
            //      - available to crops for growth
            //      - radiation to soil surface goes to SoilTemperature for heat calculations
            // calculate the Penman-Monteith potential evapotranspiration for each compartment (species x canopy layer) and potential soil water evaporation
            //      - ? crops should have supplied the non-water effects of stomatal conductance by this time (based on yesterday's states) - check
            //      - send the soil water transpiration demand back to the crops and the soil water evaporative demand to the soil water balance
            // consistent with the 2004 documentation, interception of irrigation is not considered in Arbitrator

            // Get the canopy height and depth information and break it into layers and compoents
            // FOR NOW WILL ONLY DEAL WITH A SINGLE LAYER - HEIGHT IS THE MAXIMUM HEIGHT AND DEPTH = HEIGHT
            // Create an array to hold the properties

            //for (int i = 0; i < plants.Length; i++)
            //{
            //    for (int j = 0; j < plants.Length; j++)
            //    {
            //    }
            //}
        }

        //[EventSubscribe("DoWaterArbitration")]
        private void Old_OnDoWaterArbitration()
        {
            //ToDO
            // Actual soil water evaporation
            //

            // use i for the plant loop and j for the layer loop

            double tempSupply = 0.0;  // this zeros the variable for each crop - calculates the potentialSupply for the crop for all layers - will be used to compare against demand
            // calculate the potentially available water and sum the demand
            for (int i = 0; i < plants.Length; i++)
            {
                demandWater[i] = plants[i].demandWater; // note that eventually demandWater will be calculated above in the EnergyArbitration 
                tempSupply = 0.0;
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    // this step gives the proportion of the root zone that is this layer
                    potentialSupplyWaterPlantLayer[i, j] = plants[i].RootProperties.RootExplorationByLayer[j] * plants[i].RootProperties.KL[j] * Math.Max(0.0, (Soil.SoilWater.sw_dep[j] - plants[i].RootProperties.LowerLimitDep[j]));
                    tempSupply += potentialSupplyWaterPlantLayer[i, j]; // temporary add up the supply of water across all layers for this crop, then scale back if needed below
                }
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    // if the potential supply calculated above is greater than demand then scale it back - note that this is still a potential supply as a solo crop
                    potentialSupplyWaterPlantLayer[i, j] = potentialSupplyWaterPlantLayer[i, j] * Math.Min(1.0, Utility.Math.Divide(demandWater[i], tempSupply, 0.0));
                }
            }

            // calculate the maximum amount of water available in each layer
            double[] totalAvailableWater;
            totalAvailableWater = new double[Soil.SoilWater.dlayer.Length];
            for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
            {
                totalAvailableWater[j] = Math.Max(0.0, Soil.SoilWater.sw_dep[j] - Soil.SoilWater.ll15_dep[j]);
            }

            // compare the potential water supply against the total available water
            // if supply exceeds demand then satisfy all demands, otherwise scale back by relative demand
            double[] dltSWdep = new double[Soil.SoilWater.dlayer.Length];   // to hold the changes in soil water depth
            for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++) // loop through the layers in the outer loop
            {
                for (int i = 0; i < plants.Length; i++)
                {
                    uptakeWaterPlantLayer[i, j] = potentialSupplyWaterPlantLayer[i, j] * Math.Min(1.0, Utility.Math.Divide(totalAvailableWater[j], Utility.Math.Sum(demandWater), 0.0));
                    dltSWdep[j] += -1.0 * uptakeWaterPlantLayer[i, j];  // -ve to reduce water content in the soil
                }
            }  // close the layer loop

            // send the actual transpiration to the plants

            for (int i = 0; i < plants.Length; i++)
            {
                double[] dummyArray = new double[Soil.SoilWater.dlayer.Length];  // have to create a new array for each plant to avoid the .NET pointer thing
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)           // cannot set a particular dimension from a 2D arrary into a 1D array directly so need a temporary variable
                {
                    dummyArray[j] = uptakeWaterPlantLayer[i, j];
                }
                //tempDepthArray.CopyTo(plants[i].uptakeWater, 0);  // need to use this because of the thing in .NET about pointers not values being set for arrays - only needed if the array is not newly created
                plants[i].uptakeWater = dummyArray;
                // debugging into SummaryFile
                //Summary.WriteMessage(FullPath, "Arbitrator is setting the value of plants[" + i.ToString() + "].uptakeWater(3) to  " + plants[i].uptakeWater[3].ToString());
            }

            // send the change in soil water to the soil water module
            Soil.SoilWater.dlt_sw_dep = dltSWdep;
        }

        //[EventSubscribe("DoNutrientArbitration")]
        private void Old_OnDoNutrientArbitration()
        {
            // use i for the plant loop and j for the layer loop

            NitrogenChangedType NUptakeType = new NitrogenChangedType();
            NUptakeType.Sender = Name;
            NUptakeType.SenderType = "Plant";
            NUptakeType.DeltaNO3 = new double[Soil.SoilWater.dlayer.Length];
            NUptakeType.DeltaNH4 = new double[Soil.SoilWater.dlayer.Length];

            double tempSupply = 0.0;  // this zeros the variable for each crop - calculates the potentialSupply for the crop for all layers - will be used to compare against demand
            // calculate the potentially available water and sum the demand
            for (int i = 0; i < plants.Length; i++)
            {
                if (NutrientUptakeMethod == 2)
                {
                    demandNitrogen[i] = Math.Min(plants[i].demandNitrogen, Plants[i].RootProperties.MaximumDailyNUptake); //HEB added in Maximum daily uptake constraint
                }
                else
                {
                    demandNitrogen[i] = plants[i].demandNitrogen; 
                }

                tempSupply = 0.0;  // this is the sum of potentialSupplyNitrogenPlantLayer[i, j] across j for each plant - used in the limitation to demand
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    if (NutrientUptakeMethod == 1) // use the soil KL with no water effect
                    {
                        potentialSupplyNitrogenPlantLayer[i, j] = Utility.Math.Divide(plants[i].RootProperties.KL[j], plants.Length, 0.0)
                                                                * (Soil.SoilNitrogen.no3[j] + Soil.SoilNitrogen.nh4[j])
                                                                * plants[i].RootProperties.RootExplorationByLayer[j];
                        tempSupply += potentialSupplyNitrogenPlantLayer[i, j]; // temporary add up the supply of water across all layers for this crop, then scale back if needed below

                        double tempNO3OnlySupply = Utility.Math.Divide(plants[i].RootProperties.KL[j], plants.Length, 0.0)
                                                 * Soil.SoilNitrogen.no3[j]
                                                 * plants[i].RootProperties.RootExplorationByLayer[j];
                        potentialSupplyPropNO3PlantLayer[i, j] = Utility.Math.Divide(tempNO3OnlySupply, potentialSupplyNitrogenPlantLayer[i, j], 0.0);
                    }
                    else if (NutrientUptakeMethod == 2)
                    {
                        //Fixme.  The PMF implementation was not adjusting N Uptake in partially rooted layer.  I have left it as this for testing but will need to introduce it soon.
                        //method from PMF - based on concentration  
                        //HEB.  I have rewritten this section of code to make it function identically to current PMF implementation for purposes of testing

                        double relativeSoilWaterContent = 0;
                        if (Plants[i].RootProperties.RootLengthDensityByVolume[j] > 0.0) // Test to see if there are roots in this layer.
                        {

                            relativeSoilWaterContent = (Soil.SoilWater.sw_dep[j] - Soil.SoilWater.ll15_dep[j]) / (Soil.SoilWater.dul_dep[j] - Soil.SoilWater.ll15_dep[j]);
                            relativeSoilWaterContent = Utility.Math.Constrain(relativeSoilWaterContent, 0, 1.0);
                            potentialSupplyNitrogenPlantLayer[i, j] = Soil.SoilNitrogen.no3ppm[j] * Soil.SoilNitrogen.no3[j] * plants[i].RootProperties.KNO3 * relativeSoilWaterContent;
                        }
                        else
                        {
                            potentialSupplyNitrogenPlantLayer[i, j] = 0;
                        }

                        double tempNO3OnlySupply = 0;
                        tempNO3OnlySupply = potentialSupplyNitrogenPlantLayer[i, j];
                        potentialSupplyNitrogenPlantLayer[i, j] += Soil.SoilNitrogen.nh4ppm[j] * Soil.SoilNitrogen.nh4[j] * plants[i].RootProperties.KNH4 * relativeSoilWaterContent;
                        tempSupply += potentialSupplyNitrogenPlantLayer[i, j]; // temporary add up the supply of water across all layers for this crop, then scale back if needed below

                        if (potentialSupplyNitrogenPlantLayer[i, j] == 0)
                            potentialSupplyPropNO3PlantLayer[i, j] = 1;
                        else
                            potentialSupplyPropNO3PlantLayer[i, j] = tempNO3OnlySupply / potentialSupplyNitrogenPlantLayer[i, j];
                    }
                    else if (NutrientUptakeMethod == 3)
                    {
                        //method from OilPlam - based on amount
                        double relativeSoilWaterContent = 0;
                        relativeSoilWaterContent = Utility.Math.Constrain(Utility.Math.Divide((Soil.SoilWater.sw_dep[j] - Soil.SoilWater.ll15_dep[j]), (Soil.SoilWater.dul_dep[j] - Soil.SoilWater.ll15_dep[j]), 0.0), 0.0, 1.0);

                        potentialSupplyNitrogenPlantLayer[i, j] = Math.Max(0.0, plants[i].RootProperties.RootExplorationByLayer[j] * (Utility.Math.Divide(plants[i].RootProperties.KNO3, plants.Length, 0.0) * Soil.SoilNitrogen.no3[j] + Utility.Math.Divide(plants[i].RootProperties.KNH4, plants.Length, 0.0) * Soil.SoilNitrogen.nh4[j]) * relativeSoilWaterContent);
                        tempSupply += potentialSupplyNitrogenPlantLayer[i, j]; // temporary add up the supply of water across all layers for this crop, then scale back if needed below

                        double tempNO3OnlySupply = 0;
                        tempNO3OnlySupply = Math.Max(0.0, plants[i].RootProperties.RootExplorationByLayer[j] * (Utility.Math.Divide(plants[i].RootProperties.KNO3, plants.Length, 0.0) * Soil.SoilNitrogen.no3[j]) * relativeSoilWaterContent);
                        potentialSupplyPropNO3PlantLayer[i, j] = Utility.Math.Divide(tempNO3OnlySupply, potentialSupplyNitrogenPlantLayer[i, j], 0.0);
                    }
                    else
                    {
                        throw new Exception("Invalid potential uptake method selected");
                    }
                }
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
                {
                    // if the potential supply calculated above is greater than demand then scale it back - note that this is still a potential supply as a solo crop
                    potentialSupplyNitrogenPlantLayer[i, j] = potentialSupplyNitrogenPlantLayer[i, j] * Math.Min(1.0, Utility.Math.Divide(demandNitrogen[i], tempSupply, 0.0));
                }
            }  // close the plants loop - by here have the potentialSupply by plant and layer.  This is the sole plant demand - no competition yet

            // calculate the maximum amount of nitrogen available in each layer
            double[] totalAvailableNitrogen;
            totalAvailableNitrogen = new double[Soil.SoilWater.dlayer.Length];
            for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)
            {
                totalAvailableNitrogen[j] = Soil.SoilNitrogen.no3[j] + Soil.SoilNitrogen.nh4[j];
            }

            // compare the potential nitrogen supply against the total available nitrogen
            // if supply exceeds demand then satisfy all demands, otherwise scale back by relative demand
            for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++) // loop through the layers in the outer loop
            {
                for (int i = 0; i < plants.Length; i++)
                {
                    uptakeNitrogenPlantLayer[i, j] = potentialSupplyNitrogenPlantLayer[i, j]
                                                   * Utility.Math.Constrain(Utility.Math.Divide(Utility.Math.Sum(totalAvailableNitrogen), Utility.Math.Sum(demandNitrogen), 0.0), 0.0, 1.0);
                    uptakeNitrogenPropNO3PlantLayer[i, j] = 0.0;
                    NUptakeType.DeltaNO3[j] += -1.0 * uptakeNitrogenPlantLayer[i, j] * potentialSupplyPropNO3PlantLayer[i, j];  // -ve to reduce water content in the soil
                    NUptakeType.DeltaNH4[j] += -1.0 * uptakeNitrogenPlantLayer[i, j] * (1.0 - potentialSupplyPropNO3PlantLayer[i, j]);  // -ve to reduce water content in the soil

                    if ((i == plants.Length - 1) && (j == 0))
                    {
                        Summary.WriteMessage(FullPath, potentialSupplyNitrogenPlantLayer[0, j] + " " + potentialSupplyNitrogenPlantLayer[i, j] + " " + NUptakeType.DeltaNO3[j]);
                    }
                }
            }  // close the layer loop

            for (int i = 0; i < plants.Length; i++)
            {
                double[] dummyArray1 = new double[Soil.SoilWater.dlayer.Length];  // have to create a new array for each plant to avoid the .NET pointer thing
                double[] dummyArray2 = new double[Soil.SoilWater.dlayer.Length];  // have to create a new array for each plant to avoid the .NET pointer thing
                for (int j = 0; j < Soil.SoilWater.dlayer.Length; j++)           // cannot set a particular dimension from a 2D arrary into a 1D array directly so need a temporary variable
                {
                    dummyArray1[j] = uptakeNitrogenPlantLayer[i, j];
                    dummyArray2[j] = uptakeNitrogenPropNO3PlantLayer[i, j];
                }
                double testingvalue = Utility.Math.Sum(dummyArray1);
                plants[i].uptakeNitrogen = dummyArray1;
                plants[i].uptakeNitrogenPropNO3 = dummyArray2;
                // debugging into SummaryFile
                //Summary.WriteMessage(FullPath, "Arbitrator is setting the value of plants[" + i.ToString() + "].supplyWater(3) to  " + plants[i].supplyWater[3].ToString());
            }


            // send the change in soil soil nitrate and ammonium to the soil nitrogen module

            if (NitrogenChanged != null)
                NitrogenChanged.Invoke(NUptakeType);

        }
    }
}