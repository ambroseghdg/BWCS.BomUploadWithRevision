namespace BWCS.BOMUpload.BLL
{
    public class Item
    {
        /// <summary>
        /// Get all BOM components for the item number for upload.
        /// </summary>
        public string ItemNumber { get; set; }

        /// <summary>
        /// Describing the item number.
        /// </summary>
        public string ItemDescription { get; set; }

        /// <summary>
        /// Complete description about the item number.
        /// </summary>
        public string CompleteDescription { get; set; }

        /// <summary>
        /// Drawing Number.
        /// </summary>
        public string DrawingNumber { get; set; }

        /// <summary>
        /// User Sequence number of the BOM component - editable.
        /// </summary>
        public string UserSequence { get; set; }

        /// <summary>
        /// Component Item Number.
        /// </summary>
        public string ComponentItemNumber { get; set; }

        /// <summary>
        /// Quantity of the component - editable.
        /// </summary>
        public string Qty { get; set; }

        /// <summary>
        /// Describing the component.
        /// </summary>
        public string ComponentDescription { get; set; }

        /// <summary>
        /// Extended Description 1 of the component.
        /// </summary>
        public string ExtendedDescrption1 { get; set; }

        /// <summary>
        /// Extended Description 2 of the component.
        /// </summary>
        public string ExtendedDescrption2 { get; set; }

        /// <summary>
        /// Concatednated string of componentDescritpion, extendedDescription1, extendedDescription2
        /// </summary>
        public string CompleteComponentDescription { get; set; }

        /// <summary>
        /// Revision of the component.
        /// </summary>
        public string Revision { get; set; }

        /// <summary>
        /// Unit of Measure.
        /// </summary>
        public string UnitOfMeasure { get; set; }

        /// <summary>
        /// Item Type.
        /// </summary>
        public string ItemType { get; set; }

        /// <summary>
        /// Manufacturer.
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// Description about the manufacturer.
        /// </summary>
        public string ManufacturerDescription { get; set; }

        /// <summary>
        /// Unique identifier for the BOM components - XA Token
        /// </summary>
        public string MasterCode { get; set; }

        /// <summary>
        /// Site.
        /// </summary>
        public string Site { get; set; }

        /// <summary>
        /// Parent Item Number.
        /// </summary>
        public string ParentItemNumber { get; set; }

        /// <summary>
        /// Parent Item Revision.
        /// </summary>
        public string ParentItemRevision { get; set; }

        /// <summary>
        /// Alternate BOM ID
        /// </summary>
        public string AlternateBOMId { get; set; }

        /// <summary>
        /// Component Item Number.
        /// </summary>
        public string ComponentItem { get; set; }

        /// <summary>
        /// Component Item Revision.
        /// </summary>
        public string ComponentItemRevision { get; set; }

        /// <summary>
        /// Environment.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// This flag decides whether the item exists in XA or not.
        /// Green - Exits ; Red - Not exits;
        /// </summary>
        public string Flag { get; set; }

        // Delete unchecked rows 
        public bool DontDelete { get; set; }  

        /// <summary>
        /// Action decides on the operation to be initiated on PDM data.
        /// PDM data is the needed change.
        /// Item from PDM exists in XA but not attached to the Parent Item - Add.
        /// Item from PDM exists in XA and attached to the Parent Item with change to user sequence and qty - Change
        /// Item is attached to parent item but does not exists in PDM data - Delete
        /// Item from PDM is not a valid item in XA - Skipped
        /// Item from PDM exists in XA and attached to the Parent Item with no change to user sequence and qty - No Change          
        /// </summary>
        /// 
        public string Action { get; set; }

        /// <summary>
        /// Action Note
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// Sort Action as below
        /// Add
        /// Change
        /// Delete
        /// Skipped
        /// No Change
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Set status to success when Create/ Modify/ Delete component passes successfully.
        /// Set status to failure when Create/ Modify/ Delete component fails. 
        /// </summary>
        public string Status { get; set; }
        public int IsBalloonChanged { get; set; } 
        public int IsQuantityChanged { get; set; } 
    }
}
