// Root3Power Conduit Manager

r3p_conduit : dialog {
  label = "Root3Power Conduit Manager";
  : row {
    : boxed_column {
      label = "Settings";
      : edit_box { key = "eb_prefix"; label = "Tag Prefix"; }
      : edit_box { key = "eb_next";   label = "Next Number"; }
      : edit_box { key = "eb_allow";  label = "Allowance %"; }
      : edit_box { key = "eb_round";  label = "Round Increment"; }
      : edit_box { key = "eb_text";   label = "Text Height"; }
      : toggle   { key = "tog_ftin";  label = "Show Ft-In"; value = "1"; }
    }
    : boxed_column {
      label = "Actions";
      : button { key = "btn_addsel"; label = "Measure + Add"; }
      : button { key = "btn_place";  label = "Place Tag for Selected"; }
      : button { key = "btn_route";  label = "Route 2 Points"; }
      : button { key = "btn_export"; label = "Export CSV"; }
    }
  }
  : boxed_column {
    label = "Measured Items";
    : list_box { key = "lb_items"; width = 70; height = 12; }
    : row {
      : button { key = "btn_zoom";    label = "Zoom To"; }
      : button { key = "btn_remove";  label = "Remove"; }
      : button { key = "btn_refresh"; label = "Refresh"; }
    }
  }
  ok_cancel;
}

