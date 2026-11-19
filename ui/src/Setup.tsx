import { useCallback, useEffect, useRef, useState } from "react";
import { api, type Me, type TeacherClass } from "./api";
import { useAccess } from "./access";
import { navigate } from "./router";
import { RecipientSettings } from "./PaymentQr";
import { PasteStudents } from "./PasteStudents";
import { rememberStep } from "./setup-progress";
import type { Enrollment, Student } from "./roster-api";

const titles = {
  1: "Tạo lớp học đầu tiên",
  2: "Tài khoản nhận tiền",
  3: "Thêm học sinh",
} as const;

/**
 * First-run setup, one task per screen. Step 1 (the class, its schedule and fee) is required; steps 2 and 3
 * can be left for later. What is already done is read from real data, so reopening lands on the right step.
 */
export function Setup({
  me,
  step,
  classes,
  creating,
  onCreate,
}: {
  me: Me;
  step: 1 | 2 | 3;
  classes: TeacherClass[];
  creating: boolean;
  onCreate: () => void;
}) {
  const { canWrite, denied } = useAccess(me);
  const [recipientSaved, setRecipientSaved] = useState(false);
  const first = classes[0];
  const heading = useRef<HTMLHeadingElement>(null);
  const offered = useRef(false);

  useEffect(() => {
    rememberStep(me.email, step);
    heading.current?.focus();
  }, [me.email, step]);
  // A teacher with no class is taken straight into the creation dialog once; closing it leaves the button.
  useEffect(() => {
    if (step === 1 && !first && !creating && !offered.current && canWrite) {
      offered.current = true;
      onCreate();
    }
  }, [step, first, creating, canWrite, onCreate]);

  const go = (to: 1 | 2 | 3) => navigate({ name: "setup", step: to });
  const finish = () => {
    rememberStep(me.email, null);
    navigate({ name: "today" });
  };

  return (
    <section className="setup" aria-labelledby="setup-title">
      <div
        className="setup-progress"
        role="progressbar"
        aria-label="Tiến độ thiết lập"
        aria-valuemin={1}
        aria-valuemax={3}
        aria-valuenow={step}
        aria-valuetext={`Bước ${step}/3`}
      >
        <span>Bước {step}/3</span>
        <div aria-hidden="true">
          <i style={{ width: `${(step / 3) * 100}%` }} />
        </div>
      </div>
      <h1 id="setup-title" tabIndex={-1} ref={heading}>
        {titles[step]}
      </h1>

      {step === 1 && (
        <>
          {first ? (
            <>
              <p className="notice success" role="status">
                Đã tạo lớp “{first.name}”. Lịch học và học phí nếu còn thiếu thì
                bổ sung sau trong chi tiết lớp.
              </p>
              <div className="setup-actions">
                <button className="primary" onClick={() => go(2)}>
                  Tiếp theo
                </button>
              </div>
            </>
          ) : (
            <>
              <p>
                Đặt tên lớp, chọn lịch học trong tuần và học phí mỗi buổi. Các
                buổi học dự kiến sẽ xuất hiện ở màn Hôm nay.
              </p>
              <div className="setup-actions">
                <button
                  className="primary"
                  disabled={!canWrite || creating}
                  onClick={onCreate}
                >
                  Tạo lớp
                </button>
              </div>
            </>
          )}
        </>
      )}

      {step === 2 && (
        <>
          <p>
            Học sinh sẽ chuyển học phí vào tài khoản này. Bạn có thể bỏ qua và
            thiết lập sau.
          </p>
          <RecipientSettings
            canWrite={canWrite}
            onDenied={denied}
            onChanged={(r) => setRecipientSaved(r !== null)}
          />
          <div className="setup-actions">
            <button onClick={() => go(1)}>Quay lại</button>
            <button
              className={recipientSaved ? "primary" : undefined}
              onClick={() => go(3)}
            >
              {recipientSaved ? "Tiếp theo" : "Để sau"}
            </button>
          </div>
        </>
      )}

      {step === 3 && (
        <StudentsStep
          classroom={first}
          canWrite={canWrite}
          onDenied={denied}
          onBack={() => go(2)}
          onFinish={finish}
        />
      )}
    </section>
  );
}

function StudentsStep({
  classroom,
  canWrite,
  onDenied,
  onBack,
  onFinish,
}: {
  classroom: TeacherClass | undefined;
  canWrite: boolean;
  onDenied: (e: unknown) => void;
  onBack: () => void;
  onFinish: () => void;
}) {
  const [students, setStudents] = useState<Student[]>([]);
  const [enrolled, setEnrolled] = useState<string[]>([]);
  const [error, setError] = useState("");
  const classId = classroom?.id;
  const load = useCallback(async () => {
    if (!classId) return;
    try {
      const [roster, profiles] = await Promise.all([
        api<Enrollment[]>(`/classes/${classId}/enrollments`),
        api<Student[]>("/students"),
      ]);
      setEnrolled(roster.map((r) => r.studentId));
      setStudents(profiles);
      setError("");
    } catch (e) {
      setError("Không tải được danh sách học sinh. Vui lòng thử lại.");
      onDenied(e);
    }
  }, [classId, onDenied]);
  useEffect(() => {
    void load();
  }, [load]);

  if (!classroom)
    return (
      <>
        <p>Cần tạo lớp trước khi thêm học sinh.</p>
        <div className="setup-actions">
          <button onClick={onBack}>Quay lại</button>
        </div>
      </>
    );
  return (
    <>
      <p>
        Dán danh sách từ bảng tính vào lớp “{classroom.name}”, mỗi dòng một học
        sinh. Bạn có thể bỏ qua và thêm sau.
      </p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <PasteStudents
        classId={classroom.id}
        students={students}
        enrolledIds={enrolled}
        canWrite={canWrite}
        onChanged={() => void load()}
        onClose={onFinish}
        onDenied={onDenied}
      />
      <div className="setup-actions">
        <button onClick={onBack}>Quay lại</button>
        <button
          className={enrolled.length > 0 ? "primary" : undefined}
          onClick={onFinish}
        >
          {enrolled.length > 0 ? "Hoàn tất" : "Để sau"}
        </button>
      </div>
    </>
  );
}
