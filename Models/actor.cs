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
    
    public partial class actor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public actor()
        {
            this.movie_actor = new HashSet<movie_actor>();
        }
    
        public int id { get; set; }
        public string actor_name { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public Nullable<int> country_id { get; set; }
        public string actor_img { get; set; }
        public string actor_list_img { get; set; }
    
        public virtual country country { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<movie_actor> movie_actor { get; set; }
    }
}
