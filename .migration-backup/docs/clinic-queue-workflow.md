# Clinic Queue Workflow — Aqlan Dental Pro

## Overview

The clinic queue system manages the patient flow from arrival through treatment completion. It consists of a staff queue management page and a TV display screen for the waiting area.

## Staff Queue Management — طابور العيادة

**Route**: `/clinic-queue`

This page is used by reception and doctors to manage today's patient queue.

### Actions by Status

| Status | Arabic | Available Actions |
|--------|--------|-------------------|
| Waiting | في الانتظار | نداء (Call), تغيير الغرفة (Change Room), إلغاء (Cancel) |
| Called | تم النداء | إدخال الغرفة (Enter Room), تغيير الغرفة, إلغاء |
| InRoom | داخل الغرفة | بدء الزيارة (Start Visit), تغيير الغرفة, إلغاء |
| InProgress | قيد المعالجة | فتح الزيارة (Open Visit), إنهاء (Complete) |
| Completed | مكتمل | View only / فتح الزيارة |
| Cancelled | ملغي | View only |

### How to Add a Patient

1. Click **إضافة للطابور** (Add to Queue) button
2. Search for the patient by name or file number
3. Optionally select a room (غرفة 1, غرفة 2, غرفة 3)
4. Click **إضافة للطابور** to confirm
5. The patient appears in the queue with status "في الانتظار"

### How to Call a Patient

1. Find the patient in the active queue list
2. Click **نداء** (Call) button
3. Select the room to call them to
4. Click **تأكيد** (Confirm)
5. The patient status changes to "تم النداء"
6. The patient appears on the TV display screen
7. If voice calling is enabled, an Arabic announcement plays

### Full Workflow

1. **Add** → Patient enters queue (Waiting)
2. **Call** → Assign room, patient appears on TV display (Called)
3. **Enter Room** → Patient goes to the room (InRoom)
4. **Start Visit** → Visit is created/linked, treatment begins (InProgress)
5. **Complete** → Treatment finished, patient leaves queue (Completed)

## TV Display — شاشة العرض

**Route**: `/clinic-display`

This is a full-screen display page shown on a TV in the waiting area. It shows:
- The most recently called patient (prominent)
- Room number
- Doctor name (if available)
- Waiting count and list
- Recently called patients (Called + InRoom)

### Connecting a Laptop to the TV

1. Connect your laptop to the TV using an HDMI cable
2. Set the TV input to the HDMI port
3. Open a browser and navigate to: `https://aqlan-dental.vercel.app/clinic-display`
4. Press **F11** to enter full-screen mode
5. The display will auto-refresh every 20 seconds

### Enabling Voice Calling

The TV display supports automatic Arabic voice announcements when a patient is called.

**How to enable:**
1. On the `/clinic-display` page, find the voice control bar near the top
2. Click **تفعيل النداء الصوتي** (Enable Voice Calling)
3. The browser may require a user click to allow audio — this button serves as the activation gesture
4. The status will change to "النداء الصوتي مفعل" (Voice calling is active)

**How to disable:**
1. Click **إيقاف النداء الصوتي** (Disable Voice Calling)

**Announcement text:**
- With name: "المريض [اسم المريض]، يرجى التوجه إلى [اسم الغرفة]"
- Without name: "صاحب الملف رقم [رقم الملف]، يرجى التوجه إلى [اسم الغرفة]"

**Important notes:**
- Voice calling only works on browsers that support the Web Speech API
- Arabic voice quality depends on the browser and device
- The same patient will NOT be announced again on refresh (repeat prevention)
- Only newly Called patients trigger announcements — Waiting, InRoom, InProgress, Completed, and Cancelled do NOT trigger voice

### Privacy Warning

The TV display is accessible without login (anonymous). It shows ONLY:
- Patient display name
- File number
- Room name
- Doctor name
- Queue status
- Called time

It does NOT show:
- Phone numbers
- Diagnosis
- Payment or balance
- Medical history
- Private notes
- Address
- Treatment details

If enhanced privacy is needed, contact the system administrator about file-number-only display mode.
