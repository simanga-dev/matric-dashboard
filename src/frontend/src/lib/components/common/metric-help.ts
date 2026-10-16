/** Shared explanations keep school, region, and subject statistics consistent. */
export const metricHelp = {
	passRate:
		'The percentage of learners who wrote the NSC examination and obtained the certificate in the displayed year. Passed ÷ learners who wrote × 100.',
	wrote:
		'Learners who sat the examination in the displayed year. This is the exam cohort, not the school’s total enrolment.',
	subjectWrote:
		'Learners who wrote this subject in the selected year. A learner can appear in more than one subject.',
	subjectPassRate:
		'The percentage of learners who achieved at least 30% in this subject. This subject threshold is different from obtaining the overall NSC certificate.',
	achieved:
		'Learners who achieved at least 30% in this subject. A dash means the result is unavailable; it does not mean zero.',
	subjectEntries:
		'The sum of learners who wrote across the displayed subjects. The same learner is counted once for each subject, so this is not a unique learner total.',
	quintile:
		'A socio-economic classification used for school funding, from Quintile 1 (poorest) to Quintile 5 (wealthiest). It is not an academic performance rating. Other codes mean the school is outside the usual 1–5 classification.',
	emis: 'The school’s identifier in the Education Management Information System. Use this number to distinguish schools with similar names.',
	centre:
		'The examination centre number used in the source reports. A school may have separate full-time and part-time examination centres.',
	points:
		'A change in percentage points, not a relative percentage change. Moving from 80% to 85% is an increase of 5 percentage points.',
	ranking:
		'Position among schools in the same province for the comparison year, ordered by NSC pass rate. Equal pass rates share a rank. This is not an overall measure of school quality.',
	coverage:
		'Schools with subject results available for the displayed year and matched to the school directory. Coverage can differ from the overall NSC statistics.',
	gateway:
		'This dataset covers the 11 gateway subjects in the DBE School Subject Report. It is not a complete list of every subject offered by a school.'
} as const;
