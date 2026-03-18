using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public static class CraneFactory
    {
        public static (ModelVisual3D visual, CraneParametersBase parameters) CreateCrane(CraneType type)
        {
            switch (type)
            {
                case CraneType.TowerCrane:
                    var towerParams = new TowerCraneParameters();
                    var towerVisual = new TowerCraneVisual3D(towerParams);
                    return (towerVisual, towerParams);

                case CraneType.MobileCrane:
                    var mobileParams = new MobileCraneParameters();
                    var mobileVisual = new MobileCraneVisual3D(mobileParams);
                    return (mobileVisual, mobileParams);

                default:
                    return (null, null);
            }
        }
    }
}