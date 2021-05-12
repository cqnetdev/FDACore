using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using System.ComponentModel;

namespace FDA
{
    class BackfillManager
    {
        private Queue<BackfillItem> _incomingQueue;
        private BackgroundWorker _backfillProcessor;
        private DBManager _dbManager;

        public class BackfillEventArgs : EventArgs
        {
            public List<RequestGroup> BackfillGroups;
            public DataRequest Request;
     

            public BackfillEventArgs(List<RequestGroup> backfillGroups,DataRequest request)
            {
                BackfillGroups = backfillGroups;
                Request = request; 
            }
        }

        public delegate void BackFillRequestHandler(object sender, BackfillEventArgs e);
        public event BackFillRequestHandler BackfillAnalysisComplete;

        public BackfillManager(DBManager dBManager)
        {
            _incomingQueue = new Queue<BackfillItem>();
            _backfillProcessor = new BackgroundWorker();
            _backfillProcessor.DoWork += _backfillProcessor_DoWork;
            _dbManager = dBManager;
        }

        public void AnalyzeResponse(DataRequest request)
        {
            List<Tag> backfillTags = new List<Tag>();

            // get the backfill time from options (default to 30 minutes)
            TimeSpan backfillLimit = new TimeSpan(0, 30, 0);


            try
            {
                FDADataPointDefinitionStructure tagDef;
                //Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Backfill manager received a completed transaction");
                // identify any tags with backfill enabled and an elapsed time over the data lapse limit
                foreach (Tag tag in request.TagList)
                {
                   Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Examining the tag " + tag.TagID.ToString(),false,true);

                    // skip any placeholder tags
                    if (tag.TagID == Guid.Empty)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " is a placeholder tag, skipping it",false,true);
                        continue;
                    }
                    
                    // get the tag definition
                    tagDef = _dbManager.GetTagDef(tag.TagID);

                    // tag definition not found, move on to the next tag
                    if (tagDef == null)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " DataPointDefinitionStructure not found",false,true);
                        continue;
                    }

                    // tag is disabled, move on to the next tag
                    if (!tagDef.DPDSEnabled)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " is not enabled, skipping it", false, true);
                        continue;
                    }

                    // tag quality is not 192, move on to the next tag
                    if (tag.Quality != 192)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " Quality =" + tag.Quality + ", skipping it", false, true);
                        continue;
                    }

                    // last chance check, make sure that the previous time isn't min date
                    if (tagDef.PreviousTimestamp == DateTime.MinValue)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " Previous Timestamp  = min date" + tag.Quality + ", skipping it", false, true);
                        continue;
                    }

                    Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Tag " + tag.TagID.ToString() + " BackfillEnabled = " + tagDef.backfill_enabled.ToString(),false,true);


                    if (tagDef.backfill_enabled)
                    {
      
                        backfillLimit = TimeSpan.FromMinutes(tagDef.backfill_data_lapse_limit);
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "The data lapse limit for tag " + tag.TagID.ToString() + " is " + backfillLimit.TotalMinutes + " minutes",false,true);


                        tag.LastRead = tagDef.LastRead.Timestamp;

                        TimeSpan gap = tagDef.LastRead.Timestamp.Subtract(tagDef.PreviousTimestamp);
                        Globals.SystemManager.LogApplicationEvent(this, "BackfillManager", "Read time is " + tagDef.LastRead.Timestamp + ", previous read time is " + tagDef.PreviousTimestamp + ", a gap of " + gap.TotalMinutes + " minutes",false,true);

                        if (gap >= backfillLimit)
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "Data gap of " + gap.ToString() + " detected for Tag " + tag.TagID + ". Requesting backfill from " + tagDef.PreviousTimestamp + " to " + tagDef.LastRead.Timestamp);
                            backfillTags.Add(tag);
                        }
                    }
                }

                // if any tags need to be backfilled, send them to the backfill processor to have requestgroups generated
                if (backfillTags.Count > 0)
                {
                    BackfillItem item = new BackfillItem(request, backfillTags);
                    lock (_incomingQueue)
                    {
                        _incomingQueue.Enqueue(item);
                    }
                    if (!_backfillProcessor.IsBusy)
                    {
                        _backfillProcessor.RunWorkerAsync();
                    }
                }
                else
                    BackfillAnalysisComplete?.Invoke(this, new BackfillEventArgs(null, request));
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error in BackfillManager.AnalyzeResponse()");
            }
        }

        private void _backfillProcessor_DoWork(object sender, DoWorkEventArgs e)
        {
            while (_incomingQueue.Count > 0)
            {
                //int step = 1;
                BackfillItem item;
                FDADataPointDefinitionStructure tagDef;

                lock (_incomingQueue)
                {
                    item = _incomingQueue.Dequeue();
                }
               

                List<RequestGroup> backfillGroupList = new List<RequestGroup>(); // this is what will be returned to the DataAcqManager 
                List<RequestGroup> TagRequestGroupList;

                //-------------------------------- new code --------------------------------------------------------------------------
                foreach (Tag tag in item.BackfillTagList)
                {
                    tagDef = _dbManager.GetTagDef(tag.TagID);

                    // tag definition not found, skip this tag
                    if (tagDef == null)
                        continue;

                    switch (tagDef.backfill_data_structure_type)
                    {
                        case 0: // ROC backfill
                            if (tagDef.DPSType.ToUpper() == "ROC")
                            {
                                TagRequestGroupList = ROC.ROCProtocol.BackfillTag(tagDef, item.Request);
                                backfillGroupList.AddRange(TagRequestGroupList);
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Tag " + tagDef.DPDUID + " has backfill_data_structure_type = " + tagDef.backfill_data_structure_type + ", which is incompatible with the " + tagDef.DPSType + " protocol. Backfill request cancelled");
                            }
                            break;
                            
                        case 1: // Intricate scadapack backfill (coming later)

                            // TO DO - Intricate scadapack backfill logic

                            break;
                            
                        default:
                            Globals.SystemManager.LogApplicationEvent(this, "", "Tag " + item.Request.TagList[0].TagID + " Backfill type " + tagDef.backfill_data_structure_type.ToString() + " is not valid.");
                            break;
                    }
                }

                // we're done with this request, send it back to the dataacqmananger along with any backfill request groups if backfill is required
                BackfillAnalysisComplete?.Invoke(this, new BackfillEventArgs(backfillGroupList, item.Request));
            }
        }
       
                
    }

    class BackfillItem
    {
        public DataRequest Request { get; set; }
        public List<Tag> BackfillTagList { get; set; }

        public BackfillItem(DataRequest request,List<Tag> backfillTagList)
        {
            Request = request;
            BackfillTagList = backfillTagList;
        }
    }
}
