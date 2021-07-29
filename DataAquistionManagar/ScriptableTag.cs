using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Scripting;
using Common;

namespace FDA
{
    public class ScriptableTag : ScriptableObject
    {
        public static List<ScriptableTag> WrapDPD(Dictionary<Guid,FDADataPointDefinitionStructure> toWrap)
        {
            List<ScriptableTag> output = new();
            foreach (FDADataPointDefinitionStructure fdaTag in toWrap.Values)
            {
                output.Add(new ScriptableTag(fdaTag));
            }

            return output;
        }
         
        private readonly FDADataPointDefinitionStructure _fdaTag;

        public override event PropertyChangedEventHandler PropertyChanged;


        public Double Value { get { return _fdaTag.LastRead.Value; }  }

        public void SetValue(Double value,int quality,DateTime timestamp,string rtdest="")
        {
            _fdaTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(value, quality, timestamp,rtdest, DataType.UNKNOWN, DataRequest.WriteMode.Insert); 
        }       

         
        public ScriptableTag(FDADataPointDefinitionStructure FDATag) : base(FDATag.ID)
        {
            _fdaTag = FDATag;
            FDATag.PropertyChanged += FDATag_PropertyChanged;
        }

        private void FDATag_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
