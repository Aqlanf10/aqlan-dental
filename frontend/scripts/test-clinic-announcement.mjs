import assert from "node:assert/strict";
import { buildAnnouncementText, formatFileNumberForSpeech, formatRoomForSpeech } from "../src/lib/clinic-display-announcement.js";

assert.equal(
  formatFileNumberForSpeech("GM2026-0022"),
  "جي إم 2026 صفر صفر 22"
);

assert.equal(
  formatRoomForSpeech("غرفة 1"),
  "الغرفة رقم 1"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "GM2026-0022", "غرفة 1"),
  "المراجع علي أحمد، صاحب الملف رقم جي إم 2026 صفر صفر 22، يرجى التوجه إلى الغرفة رقم 1"
);

assert.equal(
  buildAnnouncementText("", "GM2026-0022", "غرفة 2"),
  "المراجع، صاحب الملف رقم جي إم 2026 صفر صفر 22، يرجى التوجه إلى الغرفة رقم 2"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "GM2026-0022", ""),
  "المراجع علي أحمد، صاحب الملف رقم جي إم 2026 صفر صفر 22، يرجى التوجه إلى الاستقبال"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "GM2026-0022", "غرفة ١"),
  "المراجع علي أحمد، صاحب الملف رقم جي إم 2026 صفر صفر 22، يرجى التوجه إلى الغرفة رقم ١"
);

console.log("Clinic display reviewer announcement text verified successfully.");
