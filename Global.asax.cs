using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac.Integration.Mvc;
using Autofac;
using CinemaWeb.Models.Services;
using CinemaWeb.Models;

namespace CinemaWeb
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            var builder = new ContainerBuilder();

            // ??ng k� c�c d?ch v? trong container
            builder.RegisterType<VnPayService>().As<IVnPayService>();

            // Build container
            var container = builder.Build();

            // ??t Dependency Resolver cho ?ng d?ng s? d?ng container Autofac ?� c?u h�nh
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
        }
    }
}
