// Section Weight Validator (موديل 6 — FE-03)
// كان هذا المنطق مكررًا شبه حرفيًا Inline داخل صفحتي CreateSection.cshtml وEditSection.cshtml
// (نسختان منفصلتان بفروقات طفيفة غير مقصودة). تم توحيدهما بملف واحد قابل لإعادة الاستخدام.
//
// القيمة القصوى المسموح بها (الوزن المتبقي) تُقرأ من data-max-weight على حقل الإدخال نفسه بدل
// حقن قيمة Razor مباشرة داخل جافاسكربت عام — هذا يفصل بيانات العرض عن منطق التفاعل ويجعل هذا
// الملف صالحًا للاستخدام بأي صفحة تحتاج نفس السلوك دون تعديل.
//
// ملاحظة: الدالة مُعرَّفة على النطاق العام (window) عمدًا، لأن حقل الإدخال بالصفحتين يستدعيها
// مباشرة عبر السمة oninput="validateAndUpdateWeight(this)".
function validateAndUpdateWeight(input) {
    const maxAllowed = parseInt(input.dataset.maxWeight, 10) || 0;
    let val = parseInt(input.value, 10) || 0;
    const warningElement = document.getElementById('weightWarning');

    if (val > maxAllowed) {
        val = maxAllowed;
        input.value = maxAllowed;

        if (warningElement) warningElement.classList.remove('hidden');
        input.classList.add('border-red-500', 'ring-red-200');
        input.classList.remove('border-sky-200');
    } else {
        if (warningElement) warningElement.classList.add('hidden');
        input.classList.remove('border-red-500', 'ring-red-200');
        input.classList.add('border-sky-200');
    }

    const addedWeightText = document.getElementById('addedWeightText');
    if (addedWeightText) addedWeightText.innerText = val;

    const progressBarNew = document.getElementById('progressBarNew');
    if (progressBarNew) progressBarNew.style.width = val + '%';
}

document.addEventListener('DOMContentLoaded', function () {
    const weightInput = document.getElementById('weightInput');
    if (weightInput) validateAndUpdateWeight(weightInput);
});
