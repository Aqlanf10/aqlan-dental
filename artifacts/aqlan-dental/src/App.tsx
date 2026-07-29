import { lazy, Suspense, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Route, Switch, Router as WouterRouter, useLocation } from 'wouter';
import { Toaster } from '@/components/ui/toaster';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
});

// ─── Lazy route components ───────────────────────────────────────────────────

// Auth
const LoginPage = lazy(() => import('@/app/(auth)/login/page'));
const ForgotPasswordPage = lazy(() => import('@/app/(auth)/forgot-password/page'));
const ChangePasswordPage = lazy(() => import('@/app/(auth)/change-password/page'));
const ResetPasswordPage = lazy(() => import('@/app/(auth)/reset-password/page'));

// Dashboard (use clean paths without parentheses to avoid URL-encoding issues with lazy())
const DashboardLayout = lazy(() => import('@/layouts/DashboardLayout'));
const DashboardPage = lazy(() => import('@/app/(dashboard)/page'));
const DailyOperationsPage = lazy(() => import('@/app/(dashboard)/daily-operations/page'));
const AppointmentsPage = lazy(() => import('@/app/(dashboard)/appointments/page'));
const PatientsPage = lazy(() => import('@/app/(dashboard)/patients/page'));
const PatientNewPage = lazy(() => import('@/app/(dashboard)/patients/new/page'));
const PatientDetailPage = lazy(() => import('@/app/(dashboard)/patients/[id]/page'));
const DoctorsPage = lazy(() => import('@/app/(dashboard)/doctors/page'));
const BranchesPage = lazy(() => import('@/app/(dashboard)/branches/page'));
const SchedulePage = lazy(() => import('@/app/(dashboard)/schedule/page'));
const DoctorClinicPage = lazy(() => import('@/app/(dashboard)/doctor-clinic/page'));
const OrthoPage = lazy(() => import('@/app/(dashboard)/ortho/page'));
// @ts-ignore — redirect page, void return is intentional
const OrthoSurgicalPage = lazy(() => import('@/app/(dashboard)/ortho-surgical/page'));
const CephPage = lazy(() => import('@/app/(dashboard)/ceph/page'));
const LabPage = lazy(() => import('@/app/(dashboard)/lab/page'));
const InventoryPage = lazy(() => import('@/app/(dashboard)/inventory/page'));
const FinancePage = lazy(() => import('@/app/(dashboard)/finance-v3/page'));
// @ts-ignore — redirect page, void return is intentional
const HrPage = lazy(() => import('@/app/(dashboard)/hr/page'));
const EmployeesPage = lazy(() => import('@/app/(dashboard)/employees/page'));
const MessagesPage = lazy(() => import('@/app/(dashboard)/messages/page'));
const WhatsAppPage = lazy(() => import('@/app/(dashboard)/whatsapp/page'));
const SmsPage = lazy(() => import('@/app/(dashboard)/sms/page'));
const PrescriptionsPage = lazy(() => import('@/app/(dashboard)/prescriptions/page'));
const ReportsPage = lazy(() => import('@/app/(dashboard)/reports/page'));
const SettingsPage = lazy(() => import('@/app/(dashboard)/settings/page'));
// @ts-ignore — redirect page, void return is intentional
const UsersPage = lazy(() => import('@/app/(dashboard)/users/page'));
// @ts-ignore — redirect page, void return is intentional
const PatientJourneyPage = lazy(() => import('@/app/(dashboard)/patient-journey/page'));
const PatientSegmentsPage = lazy(() => import('@/app/(dashboard)/patient-segments/page'));
const ClinicCommandCenterPage = lazy(() => import('@/app/(dashboard)/clinic-command-center/page'));
// @ts-ignore — redirect page, void return is intentional
const ClinicQueuePage = lazy(() => import('@/app/(dashboard)/clinic-queue/page'));
const BookingRequestsPage = lazy(() => import('@/app/(dashboard)/booking-requests/page'));
const SurgeryPage = lazy(() => import('@/app/(dashboard)/surgery/page'));
const GeneralPage = lazy(() => import('@/app/(dashboard)/general/page'));
const ReferralsPage = lazy(() => import('@/app/(dashboard)/referrals/page'));

// Radiology has sub-routes
const RadiologyOrdersPage = lazy(() => import('@/app/(dashboard)/radiology-orders/new/page').catch(() =>
  import('@/app/(dashboard)/radiology-orders/[id]/page').catch(() => ({ default: () => <div>Radiology Orders</div> }))
));

// Portal (use clean paths without parentheses to avoid URL-encoding issues with lazy())
const PortalLayout = lazy(() => import('@/layouts/PortalLayout').catch(() => ({ default: ({ children }: { children: React.ReactNode }) => <>{children}</> })));
const PortalPage = lazy(() => import('@/app/(portal)/portal/page'));
const PortalLoginPage = lazy(() => import('@/app/(portal)/portal/login/page'));
const PortalAppointmentsPage = lazy(() => import('@/app/(portal)/portal/appointments/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalFinancePage = lazy(() => import('@/app/(portal)/portal/finance/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalMessagesPage = lazy(() => import('@/app/(portal)/portal/messages/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalPrescriptionsPage = lazy(() => import('@/app/(portal)/portal/prescriptions/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalTreatmentsPage = lazy(() => import('@/app/(portal)/portal/treatments/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalProfilePage = lazy(() => import('@/app/(portal)/portal/profile/page').catch(() => ({ default: () => <div>Portal</div> })));
const PortalChangePasswordPage = lazy(() => import('@/app/(portal)/portal/change-password/page').catch(() => ({ default: () => <div>Portal</div> })));

// Public
const PublicHomePage = lazy(() => import('@/app/(public)/home/page').catch(() => ({ default: () => <div>Public Home</div> })));

// Clinic display (kiosk, no dashboard chrome)
const ClinicDisplayPage = lazy(() => import('@/app/clinic-display/page').catch(() => ({ default: () => <div>Clinic Display</div> })));

// ─── Shared loading fallback ─────────────────────────────────────────────────

function PageLoader() {
  return (
    <div className="flex h-screen items-center justify-center" style={{ background: '#eef3f9' }}>
      <div className="text-center">
        <div className="w-12 h-12 clinic-gradient rounded-2xl flex items-center justify-center mx-auto mb-4 animate-pulse">
          <svg className="w-6 h-6 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
          </svg>
        </div>
        <p className="text-gray-500 text-sm">جارٍ التحميل...</p>
      </div>
    </div>
  );
}

// ─── Dashboard wrapper — applies DashboardLayout around a page ───────────────

function WithDashboard({ Page }: { Page: React.ComponentType }) {
  return (
    <Suspense fallback={<PageLoader />}>
      <DashboardLayout>
        <Suspense fallback={<div className="p-6 text-gray-500">جارٍ التحميل...</div>}>
          <Page />
        </Suspense>
      </DashboardLayout>
    </Suspense>
  );
}

// ─── Root redirect ────────────────────────────────────────────────────────────

function RootRedirect() {
  const [, setLocation] = useLocation();
  useEffect(() => {
    setLocation('/daily-operations', { replace: true });
  }, [setLocation]);
  return <PageLoader />;
}

// ─── Router ──────────────────────────────────────────────────────────────────

function Router() {
  return (
    <Switch>
      {/* ── Auth routes ── */}
      <Route path="/login">
        <Suspense fallback={<PageLoader />}><LoginPage /></Suspense>
      </Route>
      <Route path="/forgot-password">
        <Suspense fallback={<PageLoader />}><ForgotPasswordPage /></Suspense>
      </Route>
      <Route path="/change-password">
        <Suspense fallback={<PageLoader />}><ChangePasswordPage /></Suspense>
      </Route>
      <Route path="/reset-password">
        <Suspense fallback={<PageLoader />}><ResetPasswordPage /></Suspense>
      </Route>

      {/* ── Clinic display (kiosk) ── */}
      <Route path="/clinic-display">
        <Suspense fallback={<PageLoader />}><ClinicDisplayPage /></Suspense>
      </Route>

      {/* ── Public ── */}
      <Route path="/book">
        <Suspense fallback={<PageLoader />}><PublicHomePage /></Suspense>
      </Route>

      {/* ── Patient portal ── */}
      <Route path="/portal/login">
        <Suspense fallback={<PageLoader />}><PortalLoginPage /></Suspense>
      </Route>
      <Route path="/portal/change-password">
        <Suspense fallback={<PageLoader />}><PortalChangePasswordPage /></Suspense>
      </Route>
      <Route path="/portal/appointments">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalAppointmentsPage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal/finance">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalFinancePage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal/messages">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalMessagesPage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal/prescriptions">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalPrescriptionsPage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal/treatments">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalTreatmentsPage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal/profile">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalProfilePage /></PortalLayout>
        </Suspense>
      </Route>
      <Route path="/portal">
        <Suspense fallback={<PageLoader />}>
          <PortalLayout><PortalPage /></PortalLayout>
        </Suspense>
      </Route>

      {/* ── Dashboard routes ── */}
      <Route path="/daily-operations"><WithDashboard Page={DailyOperationsPage} /></Route>
      <Route path="/appointments"><WithDashboard Page={AppointmentsPage} /></Route>
      <Route path="/patients/new"><WithDashboard Page={PatientNewPage} /></Route>
      <Route path="/patients/:id"><WithDashboard Page={PatientDetailPage} /></Route>
      <Route path="/patients"><WithDashboard Page={PatientsPage} /></Route>
      <Route path="/doctors"><WithDashboard Page={DoctorsPage} /></Route>
      <Route path="/branches"><WithDashboard Page={BranchesPage} /></Route>
      <Route path="/schedule"><WithDashboard Page={SchedulePage} /></Route>
      <Route path="/doctor-clinic"><WithDashboard Page={DoctorClinicPage} /></Route>
      <Route path="/ortho"><WithDashboard Page={OrthoPage} /></Route>
      <Route path="/ortho-surgical"><WithDashboard Page={OrthoSurgicalPage} /></Route>
      <Route path="/ceph"><WithDashboard Page={CephPage} /></Route>
      <Route path="/lab"><WithDashboard Page={LabPage} /></Route>
      <Route path="/inventory"><WithDashboard Page={InventoryPage} /></Route>
      <Route path="/finance-v3"><WithDashboard Page={FinancePage} /></Route>
      <Route path="/hr"><WithDashboard Page={HrPage} /></Route>
      <Route path="/employees"><WithDashboard Page={EmployeesPage} /></Route>
      <Route path="/messages"><WithDashboard Page={MessagesPage} /></Route>
      <Route path="/whatsapp"><WithDashboard Page={WhatsAppPage} /></Route>
      <Route path="/sms"><WithDashboard Page={SmsPage} /></Route>
      <Route path="/prescriptions"><WithDashboard Page={PrescriptionsPage} /></Route>
      <Route path="/reports"><WithDashboard Page={ReportsPage} /></Route>
      <Route path="/settings"><WithDashboard Page={SettingsPage} /></Route>
      <Route path="/users"><WithDashboard Page={UsersPage} /></Route>
      <Route path="/patient-journey"><WithDashboard Page={PatientJourneyPage} /></Route>
      <Route path="/patient-segments"><WithDashboard Page={PatientSegmentsPage} /></Route>
      <Route path="/clinic-command-center"><WithDashboard Page={ClinicCommandCenterPage} /></Route>
      <Route path="/clinic-queue"><WithDashboard Page={ClinicQueuePage} /></Route>
      <Route path="/booking-requests"><WithDashboard Page={BookingRequestsPage} /></Route>
      <Route path="/surgery"><WithDashboard Page={SurgeryPage} /></Route>
      <Route path="/general"><WithDashboard Page={GeneralPage} /></Route>
      <Route path="/referrals"><WithDashboard Page={ReferralsPage} /></Route>
      <Route path="/radiology-orders"><WithDashboard Page={RadiologyOrdersPage} /></Route>
      <Route path="/dashboard"><WithDashboard Page={DashboardPage} /></Route>

      {/* Root → redirect to daily-operations */}
      <Route path="/"><RootRedirect /></Route>

      {/* 404 */}
      <Route>
        <div className="flex h-screen items-center justify-center" style={{ background: '#eef3f9' }}>
          <div className="text-center">
            <h1 className="text-4xl font-bold text-gray-400">404</h1>
            <p className="mt-2 text-gray-500">الصفحة غير موجودة</p>
            <a href="/daily-operations" className="mt-4 inline-block text-blue-600 hover:underline">
              العودة للرئيسية
            </a>
          </div>
        </div>
      </Route>
    </Switch>
  );
}

// ─── App ─────────────────────────────────────────────────────────────────────

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <WouterRouter base={import.meta.env.BASE_URL.replace(/\/$/, '')}>
        <Router />
      </WouterRouter>
      <Toaster />
    </QueryClientProvider>
  );
}
