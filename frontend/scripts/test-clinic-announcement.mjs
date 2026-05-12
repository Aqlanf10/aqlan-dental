import assert from "node:assert/strict";
import { buildAnnouncementText, formatFileNumberForSpeech, formatRoomForSpeech } from "../src/lib/clinic-display-announcement.js";

assert.equal(
  formatFileNumberForSpeech("2020-622"),
  "اثنين صفر اثنين صفر ستة اثنين اثنين"
);

assert.equal(
  formatFileNumberForSpeech("GM2026-0022"),
  "جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين"
);

assert.equal(
  formatRoomForSpeech("غرفة 1"),
  "الغرفة رقم 1"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "2020-622", "غرفة 1"),
  "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "GM2026-0022", "غرفة 1"),
  "المراجع علي أحمد، صاحب الملف رقم جي إم اثنين صفر اثنين ستة صفر صفر اثنين اثنين، يرجى التوجه إلى الغرفة رقم 1"
);

assert.equal(
  buildAnnouncementText("", "2020-622", "غرفة 2"),
  "المراجع، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم 2"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "2020-622", ""),
  "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الاستقبال"
);

assert.equal(
  buildAnnouncementText("علي أحمد", "2020-622", "غرفة ١"),
  "المراجع علي أحمد، صاحب الملف رقم اثنين صفر اثنين صفر ستة اثنين اثنين، يرجى التوجه إلى الغرفة رقم ١"
);

console.log("Clinic display digit-by-digit announcement with reviewer name verified successfully.");
