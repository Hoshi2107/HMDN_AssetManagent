//using System;
//using System.Collections.Generic;
//using System.Web.Http.Dependencies;
//using HMS.Services;
//using HMDN_QuanLyVatTu.Controllers;

//namespace HMDN_QuanLyVatTu
//{
//    public class SimpleDependencyResolver : IDependencyResolver
//    {
//        private readonly IAuthService _authService = new AuthService();

//        public object GetService(Type serviceType)
//        {
//            if (serviceType == typeof(IAuthService))
//            {
//                return _authService;
//            }
//            if (serviceType == typeof(AuthAPIController))
//            {
//                return new AuthAPIController(_authService);
//            }
//            return null;
//        }

//        public IEnumerable<object> GetServices(Type serviceType)
//        {
//            return new List<object>();
//        }

//        public IDependencyScope BeginScope()
//        {
//            return this;
//        }

//        public void Dispose()
//        {
//            // Không có tài nguyên cần giải phóng
//        }
//    }
//}
