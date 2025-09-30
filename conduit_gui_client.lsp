;;; ------------------------------------------------------------
;;; ConduitMeasure_20.lsp
;;; Purpose : Measure conduit runs, add 20%, and report length.
;;; Commands:
;;;   CM     – Measure selected curves (LINE/ARC/POLYLINE/SPLINE/etc.),
;;;            add +20%, optionally round, and print both raw units
;;;            and ft-in formatting.
;;;   CMTAG  – Same as CM but also places a TEXT label at picked point.
;;; Notes   :
;;;   - Uses vlax-curve functions when available for true length
;;;     (handles bulged polylines / arcs correctly).
;;;   - No stub-up or extra prompts; a flat +20% is always applied.
;;;   - Optional rounding to any increment (e.g., 1.0, 0.5, 0.1, 5.0).
;;;   - Ft-in formatting auto-inf ers from INSUNITS (1=in, 2=ft).
;;; ------------------------------------------------------------

(vl-load-com)

;; ---------- Utilities ----------

(defun cm:is-curve-type (etype)
  (member etype '("LINE" "ARC" "LWPOLYLINE" "POLYLINE" "SPLINE" "ELLIPSE"))
)

(defun cm:vlobj (ename) (vlax-ename->vla-object ename))

(defun cm:curve-length (ename / obj len res)
  (setq obj (cm:vlobj ename))
  ;; Try vlax-curve API first (most accurate/robust)
  (setq res (vl-catch-all-apply
              '(lambda ()
                 (- (vlax-curve-getDistAtParam obj (vlax-curve-getEndParam obj))
                    (vlax-curve-getDistAtParam obj (vlax-curve-getStartParam obj))))))
  (if (not (vl-catch-all-error-p res))
    (setq len res)
    (progn
      ;; Fall back to Length property when available
      (if (vlax-property-available-p obj 'Length)
        (setq len (vla-get-length obj))
        (setq len 0.0)
      )
    )
  )
  len
)

(defun cm:get-number (msg def)
  (initget 6) ; numeric >=0
  (cond ((getreal (strcat msg " <" (rtos def 2 3) ">: ")))
        (def))
)

(defun cm:round-to (val inc)
  (if (<= inc 0.0) val (* (fix (+ 0.5 (/ val inc))) inc))
)

(defun cm:apply-20 (len inc)
  ;; Always add 20%, then optional rounding
  (setq len (* len 1.20))
  (if (> inc 0.0) (setq len (cm:round-to len inc)))
  len
)

(defun cm:fmt-ftin-auto (len / iu ft in)
  ;; If drawing units are inches (INSUNITS=1), convert to ft-in.
  ;; If feet (INSUNITS=2) or unitless feet, assume feet.
  (setq iu (getvar 'INSUNITS))
  (if (= iu 1)
    (progn ; inches -> ft-in
      (setq ft (fix (/ len 12.0))
            in (- len (* 12.0 ft)))
      (strcat (itoa ft) "'-" (rtos in 2 1) "\""))
    (progn ; assume feet
      (setq ft (fix len)
            in (* 12.0 (- len ft)))
      (strcat (itoa ft) "'-" (rtos in 2 1) "\""))
  )
)

;; Sum selection; returns total length (raw) and list of {ename length}
(defun cm:sum-selection (/ ss i n en e etype seg total items)
  (setq total 0.0 items '())
  (prompt "\nSelect conduit objects (lines, polylines, arcs, splines, ellipses)... ")
  (if (setq ss (ssget))
    (progn
      (setq n (sslength ss) i 0)
      (while (< i n)
        (setq en (ssname ss i)
              e  (entget en)
              etype (cdr (assoc 0 e)))
        (if (cm:is-curve-type etype)
          (progn
            (setq seg (cm:curve-length en))
            (if (> seg 0.0)
              (progn
                (setq total (+ total seg))
                (setq items (cons (list en seg) items))
              )
            )
          )
        )
        (setq i (1+ i))
      )
      (list total (reverse items))
    )
  )
)

;; ---------- Commands ----------

(defun C:CM (/ res total items inc adj ftin)
  (setq res (cm:sum-selection))
  (if (null res)
    (princ "\nNothing selected.")
    (progn
      (setq total (car res)
            items (cadr res))
      (if (<= total 0.0)
        (princ "\nNo measurable objects found.")
        (progn
          (setq inc (cm:get-number "\nRound to increment (drawing units, 0 for no rounding)" 0.0))
          (setq adj (cm:apply-20 total inc))
          (setq ftin (cm:fmt-ftin-auto adj))
          (princ (strcat
            "\nMeasured total: " (rtos total 2 3)
            "\n+20% allowance: " (rtos adj   2 3)
            "\nFt-in format  : " ftin))
        )
      )
    )
  )
  (princ)
)

(defun C:CMTAG (/ res total inc adj ftin pt)
  (setq res (cm:sum-selection))
  (if (null res)
    (princ "\nNothing selected.")
    (progn
      (setq total (car res))
      (if (<= total 0.0)
        (princ "\nNo measurable objects found.")
        (progn
          (setq inc (cm:get-number "\nRound to increment (drawing units, 0 for no rounding)" 0.0))
          (setq adj (cm:apply-20 total inc))
          (setq ftin (cm:fmt-ftin-auto adj))
          (if (setq pt (getpoint "\nPick label location: "))
            (entmakex
              (list '(0 . "TEXT")
                    (cons 10 pt)
                    (cons 40 0.18)                ; text height
                    (cons 1 (strcat "Conduit ( +20% ): " ftin
                                    "  [" (rtos adj 2 3) "]"))
                    (cons 7 (getvar 'TEXTSTYLE))
                    (cons 8 (getvar 'CLAYER))
                    (cons 50 0.0)))
          )
          (princ (strcat "\nPlaced tag: " ftin " ([" (rtos adj 2 3) "])"))
        )
      )
    )
  )
  (princ)
)

(princ "\nConduitMeasure_20 loaded. Commands: CM (measure), CMTAG (measure + place tag).")
(princ)
