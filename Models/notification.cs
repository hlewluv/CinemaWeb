//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CinemaWeb.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class notification
    {
        public int id { get; set; }
        public string content { get; set; }
        public Nullable<int> user_id { get; set; }
        public Nullable<bool> status { get; set; }
        public Nullable<System.DateTime> date_create { get; set; }
        public string sub_content { get; set; }
    
        public virtual user user { get; set; }
    }
}
